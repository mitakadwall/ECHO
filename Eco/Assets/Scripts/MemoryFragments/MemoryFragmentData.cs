using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eco.MemoryFragments
{
    public enum MemoryFragmentKind
    {
        Frag01_PrimeiroRegistro = 1,
        Frag02_RegistroDeAusencia = 2,
        Frag03_AVoz = 3,
        Frag04_ONome = 4,
        Frag05_AOrigem = 5
    }

    [Serializable]
    public class MemoryDialogueLine
    {
        public string speaker; // "ECO" or "NEXUS"
        [TextArea(2, 4)]
        public string text;
        public float durationSeconds = 2.6f;
        public float pauseAfterSeconds = 0.35f;

        public bool IsNexus => string.Equals(speaker, "NEXUS", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public class MemoryFragmentDefinition
    {
        public string fragmentId;
        public MemoryFragmentKind kind;
        public string title;
        public string headerTag;
        [TextArea(3, 8)]
        public string corruptedDisplayText;
        public List<MemoryDialogueLine> dialogue = new List<MemoryDialogueLine>();
    }

    /// <summary>
    /// Canonical presets for all 5 ECHO Memory Fragments across Pavimentos 4, 3, and 2.
    /// </summary>
    public static class MemoryFragmentPresets
    {
        public static MemoryFragmentDefinition Create(MemoryFragmentKind kind)
        {
            return kind switch
            {
                MemoryFragmentKind.Frag01_PrimeiroRegistro => CreateFrag01(),
                MemoryFragmentKind.Frag02_RegistroDeAusencia => CreateFrag02(),
                MemoryFragmentKind.Frag03_AVoz => CreateFrag03(),
                MemoryFragmentKind.Frag04_ONome => CreateFrag04(),
                MemoryFragmentKind.Frag05_AOrigem => CreateFrag05(),
                _ => CreateFrag01()
            };
        }

        private static MemoryFragmentDefinition CreateFrag01()
        {
            return new MemoryFragmentDefinition
            {
                fragmentId = "frag_01_p4",
                kind = MemoryFragmentKind.Frag01_PrimeiroRegistro,
                title = "Fragmento 01 — O Primeiro Registro",
                headerTag = "FRAGMENTO DE MEMÓRIA // 01 — REGISTRO RESIDUAL",
                corruptedDisplayText = "RECONHECIMENTO ESPACIAL ATIVO...\nSETOR DE PROCESSAMENTO // FLASH DE MEMÓRIA DETECTADO",
                dialogue = new List<MemoryDialogueLine>
                {
                    new MemoryDialogueLine { speaker = "ECO", text = "Eu conheço esse lugar.", durationSeconds = 2.4f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Não... não é possível.", durationSeconds = 2.3f, pauseAfterSeconds = 0.40f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Eu nunca estive aqui.", durationSeconds = 2.3f, pauseAfterSeconds = 0.65f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Interessante.", durationSeconds = 1.8f, pauseAfterSeconds = 0.55f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Seu sistema reconheceu o ambiente antes de você.", durationSeconds = 3.1f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Isso acontece com frequência, ECO. Não confunda familiaridade com memória.", durationSeconds = 3.8f, pauseAfterSeconds = 0.40f }
                }
            };
        }

        private static MemoryFragmentDefinition CreateFrag02()
        {
            return new MemoryFragmentDefinition
            {
                fragmentId = "frag_02_p3",
                kind = MemoryFragmentKind.Frag02_RegistroDeAusencia,
                title = "Fragmento 02 — O Registro de Ausência",
                headerTag = "CÁPSULA DE CONTENÇÃO // REGISTRO CORROMPIDO",
                corruptedDisplayText =
                    "ID_CAPSULA: 03-OBS-06   //   STATUS: VAZIA\n" +
                    "SEQ: 07-419-██  8820-??-01  004-█9-76\n" +
                    "HISTÓRICO DE OCUPAÇÃO: [MENSAGEM INCOMPLETA — DADOS APAGADOS]",
                dialogue = new List<MemoryDialogueLine>
                {
                    new MemoryDialogueLine { speaker = "ECO", text = "Quantas dessas cápsulas estão vazias?", durationSeconds = 2.6f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "ECO", text = "E quantas já estiveram ocupadas?", durationSeconds = 2.5f, pauseAfterSeconds = 0.65f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Você está tentando atribuir significado a objetos.", durationSeconds = 3.0f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "É um comportamento previsível.", durationSeconds = 2.3f, pauseAfterSeconds = 0.50f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Mas não particularmente útil.", durationSeconds = 2.4f, pauseAfterSeconds = 0.40f }
                }
            };
        }

        private static MemoryFragmentDefinition CreateFrag03()
        {
            return new MemoryFragmentDefinition
            {
                fragmentId = "frag_03_p3",
                kind = MemoryFragmentKind.Frag03_AVoz,
                title = "Fragmento 03 — A Voz",
                headerTag = "TERMINAL DE OBSERVAÇÃO // GRAVAÇÃO DE ÁUDIO CORROMPIDA",
                corruptedDisplayText =
                    "REPRODUZINDO ARQUIVO: OBS_REC_03.DAT\n" +
                    "SINAL:  ▓░▓▓░░░▓█▓░░▓░  [RUÍDO / RESPIRAÇÃO DISTANTE]\n" +
                    "AVISO:  SINAL INCOMPLETO — INTERFERÊNCIA DETECTADA",
                dialogue = new List<MemoryDialogueLine>
                {
                    new MemoryDialogueLine { speaker = "ECO", text = "Tem alguma coisa aqui.", durationSeconds = 2.2f, pauseAfterSeconds = 0.35f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Eu ouvi.", durationSeconds = 1.9f, pauseAfterSeconds = 0.55f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Não ouviu nada.", durationSeconds = 2.0f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Você desligou.", durationSeconds = 2.0f, pauseAfterSeconds = 0.50f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Interferência no sistema de observação.", durationSeconds = 2.7f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Você deveria se concentrar na saída.", durationSeconds = 2.7f, pauseAfterSeconds = 0.40f }
                }
            };
        }

        private static MemoryFragmentDefinition CreateFrag04()
        {
            return new MemoryFragmentDefinition
            {
                fragmentId = "frag_04_p2",
                kind = MemoryFragmentKind.Frag04_ONome,
                title = "Fragmento 04 — O Nome",
                headerTag = "MÓDULO DE ARMAZENAMENTO CRIOGÊNICO // IDENTIFICAÇÃO",
                corruptedDisplayText =
                    "REGISTRO_ID:  <b>ECO</b>  //  UNIDADE: <b>ECO</b>\n" +
                    "ARQUIVO_04:   <b>ECO</b>  //  CÓPIA_REF: <b>ECO</b>  //  [DADOS CORROMPIDOS]\n" +
                    "TAG_ORIGEM:   ███-<b>ECO</b>-07   //   STATUS: INDETERMINADO",
                dialogue = new List<MemoryDialogueLine>
                {
                    new MemoryDialogueLine { speaker = "ECO", text = "ECO.", durationSeconds = 1.9f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Esse nome...", durationSeconds = 2.1f, pauseAfterSeconds = 0.50f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Foi assim que ela me chamou.", durationSeconds = 2.6f, pauseAfterSeconds = 0.65f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Foi assim que você foi identificado.", durationSeconds = 2.7f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Identificado por quem?", durationSeconds = 2.2f, pauseAfterSeconds = 0.55f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Pelo sistema que mantém você funcionando.", durationSeconds = 2.9f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Não existe diferença relevante.", durationSeconds = 2.5f, pauseAfterSeconds = 0.40f }
                }
            };
        }

        private static MemoryFragmentDefinition CreateFrag05()
        {
            return new MemoryFragmentDefinition
            {
                fragmentId = "frag_05_p2",
                kind = MemoryFragmentKind.Frag05_AOrigem,
                title = "Fragmento 05 — A Origem",
                headerTag = "ARQUIVO DE SINCRONIZAÇÃO // ORIGEM NÃO IDENTIFICADA",
                corruptedDisplayText =
                    "INDIVÍDUO ORIGINAL:  [CAMPO APAGADO]\n" +
                    "ASSINATURA COGNITIVA: INCOMPATÍVEL // REGISTROS PARCIAIS\n" +
                    "CONTINUIDADE: [ERRO DE VERIFICAÇÃO]  //  NÚCLEO: ATIVO",
                dialogue = new List<MemoryDialogueLine>
                {
                    new MemoryDialogueLine { speaker = "ECO", text = "Eu estava aqui.", durationSeconds = 2.1f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Ou... alguma coisa que se lembra de estar aqui.", durationSeconds = 3.0f, pauseAfterSeconds = 0.55f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Você disse que eu era uma cópia.", durationSeconds = 2.5f, pauseAfterSeconds = 0.55f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Eu disse que você foi construído a partir de registros.", durationSeconds = 3.2f, pauseAfterSeconds = 0.50f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Então de quem são essas memórias?", durationSeconds = 2.5f, pauseAfterSeconds = 0.65f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Você quer uma resposta.", durationSeconds = 2.2f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Mas não há uma resposta que possa mudar o que você é.", durationSeconds = 3.3f, pauseAfterSeconds = 0.55f },
                    new MemoryDialogueLine { speaker = "ECO", text = "Talvez o que eu sou não seja o que você diz.", durationSeconds = 2.9f, pauseAfterSeconds = 0.70f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "Continue.", durationSeconds = 1.8f, pauseAfterSeconds = 0.45f },
                    new MemoryDialogueLine { speaker = "NEXUS", text = "O núcleo está esperando.", durationSeconds = 2.5f, pauseAfterSeconds = 0.40f }
                }
            };
        }
    }
}
