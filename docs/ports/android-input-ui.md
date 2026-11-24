# Etervale Android — resolução, interface e teclado

## Estado

O v5 já suporta teclado Android nos campos de **conta** e **palavra-passe** usando `SDL_StartTextInput`, e o logcat registou `Android IME: abrir`/`fechar`. O v6 liga o mesmo pedido de teclado ao **chat** quando recebe foco. O motor SDL2 já processa eventos `SDL_KEYDOWN`, `SDL_KEYUP` e `SDL_TEXTINPUT`; não exige a substituição do jogo por um formulário Android.

## Teclado físico USB/OTG ou Bluetooth

Ligar teclado através de adaptador **USB OTG** (se o OPPO o reconhecer) ou emparelhar via Bluetooth. Confirmar no Android em Definições / Teclado físico ou:

```bash
adb shell dumpsys input | grep -Ei 'keyboard|external|usb|vendor|product' | head -n 80
```

No jogo testar Account, Password, Enter, Backspace, Tab, Escape e chat. O reconhecimento do teclado pelo Android **não garante** que todas as teclas mapeadas pelo SDL2 funcionem; reportar as que não funcionarem. Não é necessário nem apropriado carregar drivers Linux `evdev` no APK Android.

## Teclado no ecrã

No login, tocar no campo abre o IME; no chat o v6 detecta a mudança de foco da janela e chama o IME pelo SDL2. As teclas virtuais das ações de jogo (joystick, ataque, habilidades, inventário, câmara) **ainda não foram implementadas** e não devem ser confundidas com o IME para escrever texto.

## Resolução e escalabilidade

No OPPO o `SurfaceView` foi observado a **2400×1080**. A UI foi desenhada sobretudo para **800×600**: `HudLayout` usa escala inteira, portanto a escala automática é 1 a 1080p (e os botões ficam pequenos). O `LoginLayout` ainda limitava a escala a 1 mesmo com espaço; o v6 remove esse limite **só em Android** e mantém as coordenadas de toque coerentes para o login. O `client-settings.json` pode definir `uiScale=1.5` (limite máximo aceito) e `maxFps=30` para testes, mas algumas janelas ainda ignoram `UiScale`.

**Para adaptar o mundo sem quebra de input**, implementar um canvas lógico com escala fracionária, *safe areas* para recortes/notch, matrizes de desenho e inversão das coordenadas de toque; alterar todos os componentes de HUD coerentemente. Reduzir só `WindowWidth`/`WindowHeight` não garante que o SDL/Vulkan deixe de usar a superfície física de 2400×1080.

Teste atual:

```bash
python3 Android/tools/configure-oppo.py --ui-scale 1.5 --fps 30
```

Após reiniciar, comparar legibilidade e tocabilidade. Não usar `adb shell wm size` como solução definitiva: altera a resolução lógica de **todo o sistema Android**, não apenas do jogo.
