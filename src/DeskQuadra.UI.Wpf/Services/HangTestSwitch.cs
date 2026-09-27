namespace DeskQuadra.UI.Wpf.Services;

using DeskQuadra.UI.Wpf.Properties;

/// <summary>
/// TESTE DE FOGO (Fase 5, temporário): interruptor de travamento determinístico da thread
/// da UI para validar o Botão de Pânico do Guardian em condição real de hang.
///
/// COMO USAR: com a flag <see cref="Enabled"/> ligada, o menu do tray ganha o item
/// <see cref="MenuLabel"/>; ao clicar, a thread da UI congela de verdade (igual a um
/// travamento real: o pedido gracioso do Guardian bloqueia, expiram os 2500ms e ele
/// parte para o kill por PID, restaura os ícones e se encerra junto).
///
/// COMO REMOVER (somente no final do projeto): deletar este arquivo + o bloco marcado
/// com "HangTestSwitch" em `App.CreateTrayIcon`. Nada mais referencia este módulo.
/// </summary>
internal static class HangTestSwitch
{
    /// <summary>
    /// Liga/desliga o módulo. `false` = o item some do tray e o código de teste
    /// fica inerte (só o tipo compilado permanece até a remoção definitiva).
    /// </summary>
    public const bool Enabled = true;

    /// <summary>Rótulo do item temporário no menu do tray (prefixo TESTE de propósito).</summary>
    public static string MenuLabel => Strings.HangTestMenuLabel;

    /// <summary>
    /// Congela a thread chamadora para sempre. DEVE ser chamado na thread da UI
    /// (o clique do menu do tray já roda nela) — é exatamente isso que simula o hang.
    /// </summary>
    public static void FreezeUi() => Thread.Sleep(Timeout.Infinite);
}
