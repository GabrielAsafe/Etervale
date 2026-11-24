# Etervale Android — TLS, certificado e ligação a outro servidor

## O servidor já gera a chave e o certificado

No código `Server/PristonTale.Server/Net/ServerCertificate.cs`, o Etervale lê `Network:Certificate:Path` (por omissão `data/server-certificate.pfx`) e `Network:Certificate:Password`. **Se o PFX não existir**, o servidor cria automaticamente um certificado **ECDSA P-256** autoassinado, válido por dois anos. O servidor apresenta esse certificado no handshake TLS. Não apagues o PFX existente só para gerar outro fingerprint: isso invalida o pin dos clientes atuais.

No código `Shared/PristonTale.Protocol/Transport/TlsTransport.cs` a ligação é `tcp://HOST:10009?cert=<SHA256>`. O `cert` é a **impressão digital SHA-256 do certificado público DER**, não a chave privada; o cliente verifica com comparação em tempo constante. Sem pin, um certificado não confiável só é aceite automaticamente para loopback no servidor local.

## Obter fingerprint no próprio servidor (preferido)

No diretório do servidor, onde o PFX reside:

```bash
# Para o certificado gerado automaticamente sem password:
PFX="data/server-certificate.pfx"
test -f "$PFX"
openssl pkcs12 -in "$PFX" -clcerts -nokeys -passin pass: \
  | openssl x509 -outform DER \
  | sha256sum | awk '{print toupper($1)}'
```

Se o PFX tiver password, usa a forma apropriada sem colocar a senha no histórico; por exemplo, `openssl pkcs12 -in "$PFX" -clcerts -nokeys` e deixa o OpenSSL pedir a password interativamente. A chave privada **não** deve ser copiada para o telemóvel, publicada no Git ou partilhada por mensagens.

Alternativamente, com o servidor ativo, um administrador pode confirmar nos **logs do servidor** a linha `TLS certificate ... SHA-256 ...`, que o próprio código regista.

## Extrair certificado apresentado na rede (conferir com o pin de confiança)

```bash
HOST=192.168.1.111
PORT=10009
openssl s_client -connect "$HOST:$PORT" -showcerts </dev/null 2>/dev/null \
  | openssl x509 -outform DER \
  | sha256sum | awk '{print toupper($1)}'
```

**Segurança:** a consulta pela rede, por si só, não autentica a identidade do servidor. Compara o valor com o fingerprint obtido no servidor por um canal de confiança. Se não coincidir, **não ignores** o aviso e não aceites qualquer certificado.

## Configurar o telemóvel Android

Fecha o Etervale e conserva o JSON existente. O programa `Android/tools/configure-oppo.py` faz backup e altera apenas as propriedades indicadas:

```bash
python3 Android/tools/configure-oppo.py \
  --host 192.168.1.111 --port 10009 --pin <64_DIGITOS_HEXADECIMAIS>
```

Exemplo da propriedade resultante:

```json
{"serverAddress":"tcp://192.168.1.111:10009?cert=SHA256_CERTIFICADO_REAL"}
```

O exemplo acima **não** é um pin válido; usa os 64 dígitos reais. Também podes editar o ficheiro `client-settings.json` preservando as restantes propriedades. O servidor tem de estar acessível no Wi-Fi/rota da máquina e a porta 10009 deve estar permitida na firewall.

## Renovação e implantação pública

- Ao substituir o certificado, o SHA-256 muda; atualizar o pin em todos os clientes.
- Fazer backup seguro do `.pfx` do servidor; a chave privada pertence exclusivamente ao servidor.
- Para servidores públicos, preferir certificado confiado pelo sistema para o DNS do serviço. A validação do hostname e cadeia acontece quando não se especifica um pin de desenvolvimento.
- Nunca publicar o endereço LAN do autor como default de produção, nem guardar conta/palavra-passe no Git.
