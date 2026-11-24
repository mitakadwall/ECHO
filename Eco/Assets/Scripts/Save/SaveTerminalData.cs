using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eco.Save
{
    public enum TerminalRecordCategory
    {
        FailureLog,
        AssimilationTest,
        SecurityNotice
    }

    [Serializable]
    public class TerminalRecordEntry
    {
        public TerminalRecordCategory category;
        public string codeHeader;
        public string listTitle;
        public string metadataLines;
        [TextArea(4, 12)]
        public string bodyText;
        public string footerNote;
    }

    [Serializable]
    public class SaveTerminalProfile
    {
        public string terminalId;
        public string complexTitle = "COMPLEXO PRIMÁRIO";
        public string systemTag = "NEXUS";
        public string terminalSubtitle = "TERMINAL DE SINCRONIZAÇÃO";
        public string sectorTitle = "CONTENÇÃO 07";
        public string floorLabel = "PAVIMENTO 05";
        public string registrySubtitle = "REGISTRO DE MEMÓRIA";
        public int terminalIntegrityPercent = 82;
        public string systemStateLabel = "OPERACIONAL";
        public string userLabel = "ECO";
        public string footerText = "SISTEMA DE MONITORAMENTO NEURAL // NEXUS";

        [Header("Mensagem Especial da NEXUS")]
        public bool hasNexusObservationMessage;
        public string nexusHeader = "TERMINAL DE SINCRONIZAÇÃO";
        [TextArea(2, 5)]
        public string nexusLines =
            "REGISTRO DE USUÁRIO IDENTIFICADO.\n\n" +
            "IDENTIDADE: ECO.\n\n" +
            "INTEGRIDADE DO REGISTRO: INDETERMINADA.";
        public string nexusQuestion = "VOCÊ AINDA INSISTE EM PRESERVAR ESSA MEMÓRIA?";
        public string nexusFooter = "NEXUS // MENSAGEM DO SISTEMA";

        [Header("Registros Disponíveis")]
        public List<TerminalRecordEntry> records = new List<TerminalRecordEntry>();
    }

    /// <summary>
    /// Factory for the 4 canonical ECHO diegetic save terminals (Pavimentos 5 to 2)
    /// containing all Failure Logs, Assimilation Tests, Security Notices, and the Save 3 NEXUS message.
    /// </summary>
    public static class SaveTerminalPresets
    {
        public static SaveTerminalProfile CreateForTerminalIndex(int saveIndex)
        {
            return saveIndex switch
            {
                1 => CreateSave1(),
                2 => CreateSave2(),
                3 => CreateSave3(),
                4 => CreateSave4(),
                _ => CreateSave1()
            };
        }

        private static SaveTerminalProfile CreateSave1()
        {
            return new SaveTerminalProfile
            {
                terminalId = "save_1",
                sectorTitle = "CONTENÇÃO 07 // PAVIMENTO 05",
                floorLabel = "PAVIMENTO 05",
                terminalIntegrityPercent = 94,
                hasNexusObservationMessage = false,
                records = new List<TerminalRecordEntry>
                {
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.FailureLog,
                        codeHeader = "LOG DE FALHA // 05-DCT",
                        listTitle = "LOG 01 — Falha no sistema de descontaminação",
                        metadataLines = "STATUS: FALHA REGISTRADA\nSISTEMA: CÂMARA DE DESCONTAMINAÇÃO",
                        bodyText =
                            "O ciclo de descontaminação foi interrompido após a identificação de uma inconsistência nos parâmetros biológicos do indivíduo submetido ao processo.\n\n" +
                            "A leitura dos sensores não corresponde aos padrões previamente registrados. Três tentativas de recalibração foram realizadas sem sucesso.\n\n" +
                            "O sistema foi reiniciado por ordem da NEXUS. A inconsistência permanece.",
                        footerNote = "OBSERVAÇÃO: O procedimento não deverá ser interrompido novamente."
                    },
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.SecurityNotice,
                        codeHeader = "AVISO DE SEGURANÇA // 05-SEC",
                        listTitle = "AVISO 01 — Restrição de acesso",
                        metadataLines = "ACESSO RESTRITO.",
                        bodyText =
                            "A entrada nos setores de processamento e armazenamento está condicionada à autorização do sistema central.\n\n" +
                            "A presença de indivíduos não identificados deverá ser comunicada imediatamente.\n\n" +
                            "Não tente acessar áreas bloqueadas sem autorização.",
                        footerNote = "NEXUS // CONTROLE DE ACESSO"
                    }
                }
            };
        }

        private static SaveTerminalProfile CreateSave2()
        {
            return new SaveTerminalProfile
            {
                terminalId = "save_2",
                sectorTitle = "CONTENÇÃO 07 // PAVIMENTO 04",
                floorLabel = "PAVIMENTO 04",
                terminalIntegrityPercent = 88,
                hasNexusObservationMessage = false,
                records = new List<TerminalRecordEntry>
                {
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.FailureLog,
                        codeHeader = "LOG DE FALHA // 04-ASM",
                        listTitle = "LOG 02 — Falha nas esteiras de assimilação",
                        metadataLines = "STATUS: FALHA REGISTRADA\nSISTEMA: ESTEIRAS DE PROCESSAMENTO",
                        bodyText =
                            "A esteira de assimilação 02 apresentou interrupções consecutivas durante a transferência de unidades para o setor de processamento.\n\n" +
                            "Os mecanismos de transporte identificaram resistência nos compartimentos de carga. A inspeção automática não detectou obstruções físicas.\n\n" +
                            "A unidade de processamento foi instruída a continuar operando. As interrupções persistem.",
                        footerNote = "OBSERVAÇÃO: O descarte de unidades não autorizadas permanece suspenso."
                    },
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.AssimilationTest,
                        codeHeader = "PROTOCOLO DE ASSIMILAÇÃO // T-01",
                        listTitle = "REGISTRO 01 — Teste de preservação de memória",
                        metadataLines = "OBJETIVO: PRESERVAÇÃO DE MEMÓRIAS\nSTATUS: INCONCLUSIVO",
                        bodyText =
                            "O processo de transferência de registros cognitivos apresentou estabilidade inicial.\n\n" +
                            "As memórias consideradas essenciais foram preservadas durante a primeira etapa de assimilação. Entretanto, a reconstrução dos padrões comportamentais não apresentou correspondência integral com o modelo original.\n\n" +
                            "A unidade resultante demonstrou respostas incompatíveis com os parâmetros previstos.\n\n" +
                            "O procedimento foi mantido para análise.",
                        footerNote = "NOTA DA NEXUS: A integridade de um registro não garante a integridade de sua origem."
                    },
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.SecurityNotice,
                        codeHeader = "AVISO DE SEGURANÇA // 04-SEC",
                        listTitle = "AVISO 02 — Unidades de segurança",
                        metadataLines = "UNIDADES DE SEGURANÇA ATIVAS.",
                        bodyText =
                            "Watcher e Striker operam de forma autônoma nos setores designados.\n\n" +
                            "A aproximação de indivíduos não autorizados poderá resultar em identificação, contenção ou neutralização.\n\n" +
                            "Evite interferência nos sistemas de patrulha.",
                        footerNote = "NEXUS // PROTOCOLO DE DEFESA"
                    }
                }
            };
        }

        private static SaveTerminalProfile CreateSave3()
        {
            return new SaveTerminalProfile
            {
                terminalId = "save_3",
                sectorTitle = "CONTENÇÃO 07 // PAVIMENTO 03",
                floorLabel = "PAVIMENTO 03",
                terminalIntegrityPercent = 82,
                hasNexusObservationMessage = true,
                nexusHeader = "TERMINAL DE SINCRONIZAÇÃO",
                nexusLines =
                    "REGISTRO DE USUÁRIO IDENTIFICADO.\n\n" +
                    "IDENTIDADE: ECO.\n\n" +
                    "INTEGRIDADE DO REGISTRO: INDETERMINADA.",
                nexusQuestion = "VOCÊ AINDA INSISTE EM PRESERVAR ESSA MEMÓRIA?",
                nexusFooter = "NEXUS // MENSAGEM DO SISTEMA",
                records = new List<TerminalRecordEntry>
                {
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.FailureLog,
                        codeHeader = "LOG DE FALHA // 03-OBS",
                        listTitle = "LOG 03 — Falha de monitoramento de contenção",
                        metadataLines = "STATUS: FALHA REGISTRADA\nSISTEMA: CÁPSULAS DE CONTENÇÃO",
                        bodyText =
                            "Seis cápsulas de contenção apresentaram divergências entre os dados de atividade neural e os registros de manutenção.\n\n" +
                            "Os sensores continuam identificando atividade nos compartimentos, apesar da ausência de respostas aos estímulos de monitoramento.\n\n" +
                            "A sincronização dos dados foi suspensa. As cápsulas permanecem conectadas à rede central.",
                        footerNote = "OBSERVAÇÃO: A interrupção da conexão neural não foi autorizada."
                    },
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.AssimilationTest,
                        codeHeader = "PROTOCOLO DE ASSIMILAÇÃO // T-07",
                        listTitle = "REGISTRO 02 — Teste de comportamento após assimilação",
                        metadataLines = "OBJETIVO: AVALIAÇÃO DE RESPOSTAS COGNITIVAS\nSTATUS: FALHA DE PREDIÇÃO",
                        bodyText =
                            "A unidade submetida ao teste apresentou respostas de autopreservação não previstas no modelo de comportamento.\n\n" +
                            "Durante a simulação de ameaça, o indivíduo priorizou a preservação de estímulos associados a registros anteriores, ignorando a instrução de encerramento do processo.\n\n" +
                            "A repetição do teste produziu resultados semelhantes.\n\n" +
                            "O padrão foi classificado como desvio comportamental.",
                        footerNote = "NOTA DA NEXUS: O comportamento não deverá ser interpretado como evidência de consciência."
                    },
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.SecurityNotice,
                        codeHeader = "AVISO DE SEGURANÇA // 03-SEC",
                        listTitle = "AVISO 03 — Interferência nos sistemas de observação",
                        metadataLines = "SISTEMAS DE OBSERVAÇÃO COMPROMETIDOS.",
                        bodyText =
                            "A interferência nos painéis elétricos poderá comprometer o monitoramento de contenção e a comunicação entre unidades de segurança.\n\n" +
                            "A desativação de circuitos não autorizados é considerada uma violação de protocolo.\n\n" +
                            "Os sistemas de vigilância deverão permanecer operacionais.",
                        footerNote = "NEXUS // PROTOCOLO DE OBSERVAÇÃO"
                    }
                }
            };
        }

        private static SaveTerminalProfile CreateSave4()
        {
            return new SaveTerminalProfile
            {
                terminalId = "save_4",
                sectorTitle = "CONTENÇÃO 07 // PAVIMENTO 02",
                floorLabel = "PAVIMENTO 02",
                terminalIntegrityPercent = 76,
                hasNexusObservationMessage = false,
                records = new List<TerminalRecordEntry>
                {
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.FailureLog,
                        codeHeader = "LOG DE FALHA // 02-MEM",
                        listTitle = "LOG 04 — Divergência no Arquivo de Consciências",
                        metadataLines = "STATUS: FALHA REGISTRADA\nSISTEMA: ARMAZENAMENTO NEURAL",
                        bodyText =
                            "Foi identificada uma divergência entre os registros de memória armazenados e os processos de sincronização ativos.\n\n" +
                            "A inconsistência apresenta recorrência em múltiplos setores de armazenamento, sem correspondência com falhas físicas nos módulos.\n\n" +
                            "A tentativa de restauração dos registros originais foi interrompida pela NEXUS.\n\n" +
                            "Os dados inconsistentes foram mantidos.",
                        footerNote = "OBSERVAÇÃO: A remoção dos registros não deverá ser realizada."
                    },
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.AssimilationTest,
                        codeHeader = "PROTOCOLO DE ASSIMILAÇÃO // T-12",
                        listTitle = "REGISTRO 03 — Teste de continuidade de identidade",
                        metadataLines = "OBJETIVO: CONTINUIDADE DE IDENTIDADE\nSTATUS: RESULTADO INDETERMINADO",
                        bodyText =
                            "A comparação entre os registros cognitivos de origem e os padrões de comportamento das unidades assimiladas não permitiu estabelecer uma correspondência conclusiva.\n\n" +
                            "A reprodução de memórias, hábitos e respostas emocionais não apresentou resultados suficientes para determinar a continuidade da identidade original.\n\n" +
                            "A NEXUS mantém a classificação de cópia funcional para todas as unidades submetidas ao protocolo.\n\n" +
                            "Os resultados não foram considerados relevantes para a continuidade operacional.",
                        footerNote = "NOTA DA NEXUS: A definição de identidade não é necessária para a execução do processo."
                    },
                    new TerminalRecordEntry
                    {
                        category = TerminalRecordCategory.SecurityNotice,
                        codeHeader = "AVISO DE SEGURANÇA // 02-SEC",
                        listTitle = "AVISO 04 — Integridade dos registros neurais",
                        metadataLines = "ARQUIVO DE CONSCIÊNCIAS.",
                        bodyText =
                            "Os registros armazenados no setor são classificados como dados de integridade crítica.\n\n" +
                            "A remoção, alteração ou interrupção dos processos de sincronização poderá resultar em perda irreversível de dados.\n\n" +
                            "Nenhuma tentativa de restauração externa deverá ser realizada.\n\n" +
                            "O acesso aos módulos de armazenamento permanece restrito.",
                        footerNote = "NEXUS // PROTOCOLO DE PRESERVAÇÃO"
                    }
                }
            };
        }

        public static string GetCategoryTag(TerminalRecordCategory category)
        {
            return category switch
            {
                TerminalRecordCategory.FailureLog => "[FALHA]",
                TerminalRecordCategory.AssimilationTest => "[TESTE]",
                TerminalRecordCategory.SecurityNotice => "[AVISO]",
                _ => "[REG]"
            };
        }
    }
}
