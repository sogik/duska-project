using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Microsoft.Xna.Framework;
using Duska.Screens;
using MonoGame.Extended.Screens;
using System.Diagnostics;

namespace Duska.Core
{
    public class MultiGameManager
    {
        private static MultiGameManager _instance;
        public static MultiGameManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new MultiGameManager();
                return _instance;
            }
        }

        private Dictionary<int, GameCardScreen> _ventanasJuego;
        private Dictionary<int, Socket> _socketsPartida;
        private Game _game;
        private string _usuario;
        private MainMenuScreen _lobbyPrincipal;

        private MultiGameManager()
        {
            _ventanasJuego = new Dictionary<int, GameCardScreen>();
            _socketsPartida = new Dictionary<int, Socket>();
        }

        public void Initialize(Game game, string usuario, MainMenuScreen lobby)
        {
            _game = game;
            _usuario = usuario;
            _lobbyPrincipal = lobby;
        }

        public void AbrirNuevaVentanaJuego(int partidaId, Socket socketPrincipal)
        {
            try
            {
                Debug.WriteLine($"[MULTI] Abriendo nueva ventana de juego para partida {partidaId}");

                // Crear nueva conexión para esta partida específica
                Socket nuevoSocket = CrearNuevaConexion(socketPrincipal);

                if (nuevoSocket != null)
                {
                    // Crear nueva pantalla de juego
                    var gameScreen = new GameCardScreen(_game, _usuario);
                    gameScreen.SetExistingSocket(nuevoSocket);
                    gameScreen.SetPartidaId(partidaId); // Nuevo método para identificar la partida

                    // Guardar referencia
                    _ventanasJuego[partidaId] = gameScreen;
                    _socketsPartida[partidaId] = nuevoSocket;

                    // Notificar al servidor que queremos abrir esta ventana específica
                    NotificarServidorVentanaAbierta(nuevoSocket, partidaId);

                    Debug.WriteLine($"[MULTI] Ventana de juego {partidaId} creada exitosamente");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al abrir ventana de juego: {ex.Message}");
            }
        }

        private Socket CrearNuevaConexion(Socket socketBase)
        {
            try
            {
                // Obtener información de la conexión existente
                var endpoint = socketBase.RemoteEndPoint;

                // Crear nueva conexión
                Socket nuevoSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                nuevoSocket.Connect(endpoint);

                // Autenticar con el servidor
                string mensaje = $"1/{_usuario}/reconectar_partida";
                byte[] msg = System.Text.Encoding.ASCII.GetBytes(mensaje);
                nuevoSocket.Send(msg);

                return nuevoSocket;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al crear nueva conexión: {ex.Message}");
                return null;
            }
        }

        private void NotificarServidorVentanaAbierta(Socket socket, int partidaId)
        {
            try
            {
                string mensaje = $"30/{_usuario}/{partidaId}";
                byte[] msg = System.Text.Encoding.ASCII.GetBytes(mensaje);
                socket.Send(msg);

                Debug.WriteLine($"[MULTI] Notificación de ventana enviada para partida {partidaId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al notificar ventana: {ex.Message}");
            }
        }

        public void CerrarVentanaJuego(int partidaId)
        {
            try
            {
                if (_ventanasJuego.ContainsKey(partidaId))
                {
                    _ventanasJuego[partidaId].Dispose();
                    _ventanasJuego.Remove(partidaId);
                }

                if (_socketsPartida.ContainsKey(partidaId))
                {
                    _socketsPartida[partidaId].Close();
                    _socketsPartida.Remove(partidaId);
                }

                Debug.WriteLine($"[MULTI] Ventana de juego {partidaId} cerrada");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al cerrar ventana: {ex.Message}");
            }
        }

        public List<int> GetPartidaActivas()
        {
            return new List<int>(_ventanasJuego.Keys);
        }

        public bool TienePartidaActiva(int partidaId)
        {
            return _ventanasJuego.ContainsKey(partidaId);
        }
    }
}