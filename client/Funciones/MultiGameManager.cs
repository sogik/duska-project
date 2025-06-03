using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Duska.Screens;

namespace Duska.Core
{
    public class SimpleMultiWindowManager
    {
        private static SimpleMultiWindowManager _instance;
        public static SimpleMultiWindowManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SimpleMultiWindowManager();
                return _instance;
            }
        }

        private Dictionary<int, GameCardScreen> _partidasActivas;
        private string _usuario;
        private ScreenManager _screenManager;
        private Game _game;
        private Socket _socketPrincipal; // **UN SOLO SOCKET**

        private SimpleMultiWindowManager()
        {
            _partidasActivas = new Dictionary<int, GameCardScreen>();
        }

        public void Initialize(string usuario, ScreenManager screenManager, Game game)
        {
            _usuario = usuario;
            _screenManager = screenManager;
            _game = game;
            Debug.WriteLine("[SIMPLE_MULTI] Inicializado para usuario: " + usuario);
        }

        // **NUEVO MÉTODO PARA ESTABLECER EL SOCKET PRINCIPAL**
        public void SetSocketPrincipal(Socket socket)
        {
            _socketPrincipal = socket;
            Debug.WriteLine("[SIMPLE_MULTI] Socket principal establecido");
        }

        public void AbrirNuevaVentanaPartida(int partidaId, Socket socketBase)
        {
            try
            {
                Debug.WriteLine($"[SIMPLE_MULTI] Abriendo partida {partidaId} en la misma ventana");

                if (_partidasActivas.ContainsKey(partidaId))
                {
                    Debug.WriteLine($"[SIMPLE_MULTI] La partida {partidaId} ya está abierta");

                    var pantallaExistente = _partidasActivas[partidaId];
                    _screenManager.LoadScreen(pantallaExistente, new FadeTransition(_game.GraphicsDevice, Color.Black, 0.5f));
                    return;
                }

                // **CREAR NUEVA PANTALLA DE JUEGO USANDO EL SOCKET PRINCIPAL**
                var gameScreen = new GameCardScreen(_game, _usuario);
                gameScreen.SetPartidaId(partidaId);

                // **USAR EL SOCKET PRINCIPAL, NO CREAR UNO NUEVO**
                gameScreen.SetExistingSocket(_socketPrincipal ?? socketBase);

                // **GUARDAR REFERENCIA**
                _partidasActivas[partidaId] = gameScreen;

                // **CAMBIAR A LA PANTALLA DE JUEGO**
                _screenManager.LoadScreen(gameScreen, new FadeTransition(_game.GraphicsDevice, Color.Black, 0.5f));

                Debug.WriteLine($"[SIMPLE_MULTI] Partida {partidaId} abierta correctamente");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al abrir partida: {ex.Message}");
            }
        }

        public void CerrarVentanaPartida(int partidaId)
        {
            try
            {
                if (_partidasActivas.ContainsKey(partidaId))
                {
                    _partidasActivas[partidaId]?.Dispose();
                    _partidasActivas.Remove(partidaId);
                }

                Debug.WriteLine($"[SIMPLE_MULTI] Partida {partidaId} cerrada");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al cerrar partida: {ex.Message}");
            }
        }

        public List<int> GetPartidasActivas()
        {
            return new List<int>(_partidasActivas.Keys);
        }

        public bool TienePartidaAbierta(int partidaId)
        {
            return _partidasActivas.ContainsKey(partidaId);
        }

        public void RegresarAlMenuPrincipal()
        {
            try
            {
                Debug.WriteLine("[SIMPLE_MULTI] Regresando al menú principal");

                var mainMenu = new MainMenuScreen(_game, _usuario);

                // **PASAR EL SOCKET PRINCIPAL AL MENÚ**
                if (_socketPrincipal != null)
                {
                    mainMenu.SetExistingSocket(_socketPrincipal);
                }

                _screenManager.LoadScreen(mainMenu, new FadeTransition(_game.GraphicsDevice, Color.Black, 0.5f));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error regresando al menú: {ex.Message}");
            }
        }
    }
}