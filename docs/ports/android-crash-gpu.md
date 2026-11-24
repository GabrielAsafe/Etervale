# Etervale Android — investigação do SIGABRT na Mali-G57

## Evidência de 2026-11-23

O `logcat` do OPPO CPH2065, Android 12, registou às **22:57:06.883** um `Fatal signal 6 (SIGABRT)` no processo Etervale (`SDLActivity`), thread **`mali-cmar-backe`**. O jogo já entrara no mundo. A ausência de stack de crash nativo impede concluir se foi provocado por um problema do driver, mau uso da API Vulkan, invalidação de superfície, sincronização, memória ou outro erro.

As linhas `OplusArpPeer` e `ResourcesManager` de processos diferentes não identificam a causa deste crash.

## Recolha sem perder o momento do erro

Depois da falha, **antes de limpar o logcat**:

```bash
bash Android/tools/collect-crash.sh
# ou manualmente:
adb logcat -b crash -d -v threadtime > crash-native.txt
adb logcat -b main,system,events -d -v threadtime > crash-full.txt
adb shell dumpsys activity exit-info com.etervale.game > exit-info.txt
```

Se a stack estiver ausente, `adb bugreport /caminho/para/bugreport.zip` pode incluir informação sobre o crash e um tombstone. O bugreport é grande e pode conter dados privados; revê antes de partilhar. O Android normalmente guarda tombstones em `/data/tombstones/` mas a leitura direta pode exigir permissões de sistema.

## Isolar o gatilho

1. Repetir no ecrã de login, seleção e mundo, anotar fase/tempo e padrão.
2. Comparar preset gráfico Low, `maxFps=30` e `maxFps=60`, sem mudar muitas coisas simultaneamente.
3. Verificar `adb shell dumpsys meminfo com.etervale.game` enquanto o processo está ativo.
4. Guardar stack nativa e símbolos originais de `libmgruntime.so`/BuildId, para usar `llvm-addr2line` quando a stack apontar para o motor.
5. Monitorizar mensagens `VK_ERROR_DEVICE_LOST`, `VK_ERROR_OUT_OF_DEVICE_MEMORY`, sinais de sincronização inválida ou erros do ciclo de vida da superfície. A ausência destes textos não exclui um problema nativo.
6. Testar nova entrada no mundo, trocas de campo e minimização/retoma com logs separados.

**Não desativar validação TLS nem copiar binários Linux Android como tentativa de contornar um crash Vulkan.**
