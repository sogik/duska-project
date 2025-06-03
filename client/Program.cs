using System;
using System.Linq;

namespace Duska
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // **VERIFICAR SI SE PASARON ARGUMENTOS PARA NUEVA VENTANA**
                if (args.Length > 0)
                {
                    var partidaArg = args.FirstOrDefault(a => a.StartsWith("--partida="));
                    var usuarioArg = args.FirstOrDefault(a => a.StartsWith("--usuario="));
                    var modoArg = args.FirstOrDefault(a => a.StartsWith("--modo="));

                    if (partidaArg != null && usuarioArg != null && modoArg == "--modo=juego")
                    {
                        int partidaId = int.Parse(partidaArg.Substring("--partida=".Length));
                        string usuario = usuarioArg.Substring("--usuario=".Length);

                        System.Diagnostics.Debug.WriteLine($"[PROGRAM] Iniciando ventana de juego - Partida: {partidaId}, Usuario: {usuario}");

                        // **CREAR INSTANCIA ESPECÍFICA PARA ESTA PARTIDA**
                        using (var gameInstance = new Core.GameInstanceForPartida(usuario, partidaId))
                        {
                            gameInstance.Run();
                        }
                        return;
                    }
                }

                // **FLUJO NORMAL - MENÚ PRINCIPAL**
                using (var game = new Game1())
                {
                    game.Run();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error en Program.Main: {ex.Message}");

                // Plan de respaldo - siempre intentar abrir juego normal
                using (var game = new Game1())
                {
                    game.Run();
                }
            }
        }
    }
}