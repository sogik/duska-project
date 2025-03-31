using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;

namespace client
{
    public class NetworkManager : GameComponent
    {
        private Socket _clientSocket;
        private Thread _receiveThread;
        private bool _isConnected;
        private string _lastMessage;
        private readonly string _serverIp;
        private readonly int _serverPort;

        public event Action<string> OnMessageReceived;
        public bool IsConnected => _isConnected;

        public NetworkManager(Game game, string serverIp, int serverPort) : base(game)
        {
            _serverIp = serverIp;
            _serverPort = serverPort;
            game.Components.Add(this);
        }

        public override void Initialize()
        {
            ConnectToServer();
            base.Initialize();
        }

        private void ConnectToServer()
        {
            try
            {
                IPAddress ipAddress = IPAddress.Parse(_serverIp);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, _serverPort);

                _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _clientSocket.BeginConnect(remoteEP, ConnectCallback, _clientSocket);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error de conexión: {ex.Message}");
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndConnect(ar);
                _isConnected = true;
                System.Diagnostics.Debug.WriteLine("Conectado al servidor");

                _receiveThread = new Thread(ReceiveData);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en conexión: {ex.Message}");
            }
        }

        public void SendMessage(string message)
        {
            if (!_isConnected) return;

            try
            {
                byte[] byteData = Encoding.ASCII.GetBytes(message);
                _clientSocket.BeginSend(byteData, 0, byteData.Length, 0, SendCallback, _clientSocket);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al enviar: {ex.Message}");
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                int bytesSent = client.EndSend(ar);
                System.Diagnostics.Debug.WriteLine($"Enviados {bytesSent} bytes al servidor");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en envío: {ex.Message}");
            }
        }

        private void ReceiveData()
        {
            while (_isConnected)
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    int bytesRec = _clientSocket.Receive(buffer);
                    _lastMessage = Encoding.ASCII.GetString(buffer, 0, bytesRec);
                    System.Diagnostics.Debug.WriteLine($"Recibido: {_lastMessage}");

                    OnMessageReceived?.Invoke(_lastMessage);
                }
                catch (SocketException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error de socket: {ex.Message}");
                    Disconnect();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error general: {ex.Message}");
                    Disconnect();
                }
            }
        }

        public void Disconnect()
        {
            if (!_isConnected) return;

            try
            {
                _isConnected = false;
                _clientSocket?.Shutdown(SocketShutdown.Both);
                _clientSocket?.Close();
                System.Diagnostics.Debug.WriteLine("Desconectado del servidor");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al desconectar: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            Disconnect();
            _receiveThread?.Abort();
            base.Dispose(disposing);
        }
    }
}