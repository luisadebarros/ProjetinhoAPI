#!/usr/bin/env bash
# ============================================================================
#  AULA 2 — Atacando o próprio JWT
#  Rode no Git Bash, na raiz do projeto:   bash estudos/aula02-ataques.sh
#  O script sobe a API, forja vários tokens e mostra como a API reage.
# ============================================================================
set -u
export PATH="/c/Program Files/dotnet:$PATH"
export ASPNETCORE_ENVIRONMENT=Development
B=http://localhost:5080
PROJ=src/ProjetinhoAPI
ALVO="$B/auth/me"

# ---------- funções auxiliares ----------
b64url()   { base64 -w0 | tr '+/' '-_' | tr -d '='; }                 # bytes -> base64url
b64dec()   { local s; s=$(echo -n "$1" | tr '_-' '/+'); while [ $(( ${#s} % 4 )) -ne 0 ]; do s="$s="; done; echo "$s" | base64 -d 2>/dev/null; }
assinar()  { echo -n "$1" | openssl dgst -sha256 -hmac "$2" -binary | b64url; }  # HMAC-SHA256
montar()   { # $1=header json  $2=payload json  $3=chave (vazio = sem assinatura)
  local h p; h=$(echo -n "$1" | b64url); p=$(echo -n "$2" | b64url)
  if [ -z "$3" ]; then echo "$h.$p."; else echo "$h.$p.$(assinar "$h.$p" "$3")"; fi; }
testar()   { # $1=descrição  $2=token
  local resp code motivo
  resp=$(curl -s -D - -o /dev/null -H "Authorization: Bearer $2" "$ALVO")
  code=$(echo "$resp" | head -1 | awk '{print $2}')
  motivo=$(echo "$resp" | grep -i '^WWW-Authenticate' | sed -nE 's/.*error_description="([^"]*)".*/\1/p' | tr -d '\r')
  [ -z "$motivo" ] && [ "$code" = "401" ] && motivo="token inválido/malformado"
  [ "$code" = "200" ] && cor="\e[31m" || cor="\e[32m"   # vermelho = ataque PASSOU
  printf "%-58s ${cor}%s\e[0m  %s\n" "$1" "$code" "${motivo:+($motivo)}"; }
payload()  { # monta payload com valores customizáveis
  # exp = agora + VALIDADE (negativo = já expirado); o token "nasceu" 1h antes de expirar
  local now=$(date +%s); local exp=$(( now + ${VALIDADE:-3600} )); local nbf=$(( exp - 3600 ))
  echo -n "{\"iss\":\"${ISS:-ProjetinhoAPI}\",\"aud\":\"${AUD:-ProjetinhoAPI}\",\"sub\":\"hacker\",\"unique_name\":\"${NOME:-hacker}\",\"role\":\"Admin\",\"iat\":$nbf,\"nbf\":$nbf,\"exp\":$exp}"; }
HS='{"alg":"HS256","typ":"JWT"}'

# ---------- sobe a API ----------
echo "Compilando e subindo a API..."
dotnet build $PROJ -v q -nologo > /dev/null || { echo "Falha no build"; exit 1; }
dotnet run --no-build --project $PROJ --no-launch-profile --urls $B > /dev/null 2>&1 &
for i in $(seq 1 40); do curl -s -o /dev/null $B/openapi/v1.json && break; sleep 1; done
trap 'taskkill //F //IM ProjetinhoAPI.exe >/dev/null 2>&1' EXIT

# ---------- token legítimo ----------
TOKEN=$(curl -s -X POST $B/auth/login -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"Projetinho@2026"}' | sed -E 's/.*"accessToken":"([^"]+)".*/\1/')
IFS='.' read -r H P S <<< "$TOKEN"
echo; echo "=== Ataques SEM conhecer a chave secreta ==="
testar "0) Token legítimo (controle)" "$TOKEN"

# 1) Alterar o payload e manter a assinatura antiga
P_FALSO=$(b64dec "$P" | sed 's/"unique_name":"admin"/"unique_name":"hacker"/' | b64url)
testar "1) Payload alterado, assinatura original" "$H.$P_FALSO.$S"

# 2) Ataque alg:none — "este token não precisa de assinatura"
testar "2) alg:none (sem assinatura)" "$(montar '{"alg":"none","typ":"JWT"}' "$(payload)" "")"

# 3) Assinar com uma chave chutada
testar "3) Assinado com chave chutada ('segredo123...')" "$(montar "$HS" "$(payload)" 'segredo123segredo123segredo123!!')"

# 4) Remover a assinatura de um token legítimo
testar "4) Token legítimo sem a 3a parte" "$H.$P."

# 5) Lixo
testar "5) Token qualquer ('abc.def.ghi')" "abc.def.ghi"

# ---------- agora o atacante ROUBOU a chave ----------
# Os user-secrets ficam em %APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json
SECRETS_ID=$(sed -nE 's/.*<UserSecretsId>(.*)<\/UserSecretsId>.*/\1/p' $PROJ/ProjetinhoAPI.csproj)
CHAVE=$(sed -nE 's/.*"Jwt:Key": *"([^"]+)".*/\1/p' "$APPDATA/Microsoft/UserSecrets/$SECRETS_ID/secrets.json")
[ -z "$CHAVE" ] && { echo "Não achei a Jwt:Key nos user-secrets"; exit 1; }
echo; echo "=== Ataques COM a chave secreta vazada ==="
testar "6) Token forjado com a chave verdadeira" "$(montar "$HS" "$(payload)" "$CHAVE")"
testar "7) ...mas com issuer errado"           "$(ISS=OutraAPI montar "$HS" "$(ISS=OutraAPI payload)" "$CHAVE")"
testar "8) ...mas com audience errada"         "$(montar "$HS" "$(AUD=OutraAPI payload)" "$CHAVE")"
testar "9) ...mas expirado há 10 segundos"     "$(montar "$HS" "$(VALIDADE=-10 payload)" "$CHAVE")"
testar "10) ...mas expirado há 2 minutos"      "$(montar "$HS" "$(VALIDADE=-120 payload)" "$CHAVE")"

echo; echo -e "Legenda: \e[32mverde\e[0m = API bloqueou | \e[31mvermelho\e[0m = API ACEITOU"
