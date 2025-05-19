using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Input;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Screens.Transitions;
using GeonBit.UI.Entities;
using GeonBit.UI;
using GeonBit.UI.Utils;
using System.Diagnostics;
using System.Net;

namespace Duska.Screens
{
    public class GameCardScreen : GameScreen
    {
        private Texture2D[] _cartas = new Texture2D[4];
        private Texture2D _cartaSeleccionada;
        private bool _mostrarTodas = true;
        private Random _random = new Random();
        private SpriteBatch _spriteBatch;
        private BitmapFont _fuente;
        private MonoGame.Extended.Input.KeyboardStateExtended _estadoTecladoAnterior;
        private List<Texture2D> _cartasDisponibles = new List<Texture2D>();
        private Texture2D _background;
        private BuiltinThemes _currTheme;
        private Socket server;
        private Thread atender;
        private bool isReconnecting = false;
        private volatile bool stopMessageListener = false;
        private bool conectado = false;
        private Thread messageListenerThread;

        public string usuario;

        private RichParagraph panelmensajes = new RichParagraph(@"");
        private string mensajeCartasPendiente = null;
        private object mensajeLock = new object();

        // Añade estas variables para manejar mensajes pendientes
        private List<string> mensajesPendientes = new List<string>();
        private object mensajesLock = new object();
        private bool hayNuevosMensajes = false;

        // Variable para controlar la frecuencia de los mensajes de depuración
        private int _drawCounter = 0;

        private TimeSpan tiempoDesdeUltimaSolicitud = TimeSpan.Zero;
        private TimeSpan intervaloSolicitudCartas = TimeSpan.FromSeconds(30); // 30 segundos

        // Añade esta variable de clase para mantener el historial de mensajes
        private List<string> historialChat = new List<string>();

        public GameCardScreen(Game game, string usuario) : base(game)
        {
            this.usuario = usuario;
            _cartasDisponibles = new List<Texture2D>();
            _cartas = new Texture2D[4];
            _random = new Random();
            _mostrarTodas = true;
        }

        public override void LoadContent()
        {
            try
            {
                base.LoadContent();

                // Inicializar la interfaz de usuario con un tema
                InitializeThemeAndUI(BuiltinThemes.hd);

                _spriteBatch = new SpriteBatch(GraphicsDevice);

                // Cargar texturas de cartas
                string[] cartasNombres = new string[] { "ace", "jack", "king", "queen" };
                for (int i = 0; i < cartasNombres.Length; i++)
                {
                    try
                    {
                        _cartas[i] = Content.Load<Texture2D>(cartasNombres[i]);
                        Debug.WriteLine($"Carta {cartasNombres[i]} cargada correctamente");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error al cargar la carta {cartasNombres[i]}: {ex.Message}");
                        // Crear textura en blanco como fallback
                        _cartas[i] = new Texture2D(GraphicsDevice, 100, 150);
                        Color[] data = new Color[100 * 150];
                        for (int p = 0; p < data.Length; p++)
                            data[p] = Color.White;
                        _cartas[i].SetData(data);
                    }
                }

                // Inicializar con cartas de muestra
                _cartasDisponibles.Clear();
                _cartasDisponibles.Add(_cartas[0]); // ace
                _cartasDisponibles.Add(_cartas[1]); // jack
                _cartasDisponibles.Add(_cartas[2]); // king
                _cartasDisponibles.Add(_cartas[3]); // queen
                Debug.WriteLine($"[INIT] Cartas de muestra añadidas: {_cartasDisponibles.Count}");

                // Conectar al servidor y solicitar cartas (solo una vez)
                if (conectado || ConnectToServerIfNeeded())
                {
                    GetCards(usuario);
                    StartMessageListener();
                    ChatPanel(true, "Bienvenido al juego, " + usuario);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error general en LoadContent: {ex.Message}");
            }
        }

        // Método auxiliar para verificar y establecer conexión
        private bool ConnectToServerIfNeeded()
        {
            try
            {
                if (server == null || !server.Connected)
                {
                    ConnectToServer();
                }
                return server != null && server.Connected;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al conectar: {ex.Message}");
                return false;
            }
        }

        private void InitializeThemeAndUI(BuiltinThemes theme)
        {
            _currTheme = theme;

            // Inicializar la interfaz si no está ya inicializada
            if (UserInterface.Active == null)
            {
                UserInterface.Initialize(Content, theme);
            }
            else
            {
                // Si ya está inicializada, actualizar el tema
                UserInterface.Active.Clear();
                UserInterface.Initialize(Content, theme);
            }

            _spriteBatch = _spriteBatch ?? new SpriteBatch(GraphicsDevice);

            Debug.WriteLine("Tema UI inicializado correctamente");
        }

        private void Options(bool visible)
        {
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // sliders title
            panel.AddChild(new Header("Sliders"));
            panel.AddChild(new HorizontalLine());
            panel.AddChild(new Paragraph("Sliders help pick numeric value in range:"));

            panel.AddChild(new Paragraph("\nDefault slider"));
            {
                var slider = panel.AddChild(new Slider(-10, 10, SliderSkin.Default)) as Slider;
                var valueLabel = new Label("Value: 0");
                slider.OnValueChange = (Entity entity) =>
                {
                    valueLabel.Text = "Value: " + slider.Value;
                };
                panel.AddChild(valueLabel);
            }

            panel.AddChild(new Paragraph("\nFancy slider"));
            panel.AddChild(new Slider(0, 10, SliderSkin.Fancy));

            // progressbar title
            panel.AddChild(new LineSpace(3));
            panel.AddChild(new Header("Progress bar"));
            panel.AddChild(new HorizontalLine());
            panel.AddChild(new Paragraph("Works just like sliders:"));
            panel.AddChild(new ProgressBar(0, 10));

            Button changeThemeBtn = new Button("Change Theme", ButtonSkin.Default);
            changeThemeBtn.OnClick = (Entity entity) =>
            {
                int theme = (int)_currTheme + 1;
                if (theme > (int)BuiltinThemes.editor)
                {
                    theme = 0;
                }
                InitializeThemeAndUI((BuiltinThemes)theme);
                //Options(true);
            };
            changeThemeBtn.ToolTipText = "Rotate through the built-in themes.";
            panel.AddChild(changeThemeBtn);

            // Ejemplo: Agregar un botón para regresar al menú principal
            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
            };
            panel.AddChild(backBtn);
        }

        private void EscMenu(string usuario, bool visible)
        {
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            panel.AddChild(new Header("Pause Menu"));
            panel.AddChild(new HorizontalLine());

            Button resumeBtn = new Button("Resume", ButtonSkin.Default);
            resumeBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
            };
            panel.AddChild(resumeBtn);

            Button optionsBtn = new Button("Options", ButtonSkin.Default);
            optionsBtn.OnClick = (Entity btn) =>
            {
                Options(true);
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
            };
            panel.AddChild(optionsBtn);

            Button ExitBtn = new Button("Exit", ButtonSkin.Default);
            ExitBtn.OnClick = (Entity btn) =>
            {
                int estado = this.estado(usuario, "0");
                Debug.WriteLine("Estado enviado: " + estado);
                DisconnectFromServer();
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
                Game.Exit();
            };
            panel.AddChild(ExitBtn);
        }

        // Reemplaza el método ProcessServerMessage por este código:
        private void ProcessServerMessage(string message)
        {
            try
            {
                Debug.WriteLine($"[SERVER] Procesando mensaje: {message}");

                if (message.StartsWith("CARDS/"))
                {
                    // En lugar de procesar las cartas directamente,
                    // añadamos el mensaje a la cola pendiente para que
                    // sea procesado en el hilo principal (Update)
                    lock (mensajesLock)
                    {
                        mensajesPendientes.Add(message);
                        hayNuevosMensajes = true;
                        Debug.WriteLine($"[SERVER] Mensaje de cartas añadido a la cola: {message}");
                    }
                }
                else if (message.StartsWith("CHAT/"))
                {
                    // Mensaje de chat
                    string chatMessage = message.Substring(5);
                    Debug.WriteLine("Mensaje de chat recibido: " + chatMessage);

                    ChatPanel(true, chatMessage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVER] Error procesando mensaje: {ex.Message}");
            }
        }

        private void ProcesarCartasDelServidor(string cartasString)
        {
            Debug.WriteLine($"[CARTAS] Procesando: {cartasString}");

            try
            {
                string[] partes = cartasString.Split('/');

                // Verificar que tenemos el formato correcto
                if (partes.Length < 5 || !partes[0].Equals("CARDS", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[CARTAS] Formato incorrecto: {cartasString}");
                    return;
                }

                // IMPORTANTE: Crear una nueva lista temporal
                List<Texture2D> cartasTemp = new List<Texture2D>();

                // Procesar cada tipo de carta
                for (int i = 0; i < 4; i++)
                {
                    if (int.TryParse(partes[i + 1], out int cantidad))
                    {
                        Debug.WriteLine($"[CARTAS] Tipo {i}: {cantidad} cartas");

                        for (int j = 0; j < cantidad; j++)
                        {
                            if (_cartas[i] != null)
                            {
                                cartasTemp.Add(_cartas[i]);
                                Debug.WriteLine($"[CARTAS] Añadida carta tipo {i} (total: {cartasTemp.Count})");
                            }
                        }
                    }
                }

                // Si llegamos hasta aquí, actualizar la lista real
                _cartasDisponibles = cartasTemp;
                Debug.WriteLine($"[CARTAS] Proceso completo. Total cartas: {_cartasDisponibles.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CARTAS] Error: {ex.Message}");
            }
        }

        private void GetCards(string usuario)
        {
            try
            {
                ConnectToServer();
                string mensaje = "9/" + usuario + "/";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);
                Debug.WriteLine("[SERVER] Solicitud de cartas enviada: " + mensaje);
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ERROR] Error al solicitar cartas: " + ex.Message);
            }
        }

        private int estado(string usuario, string estado)
        {
            try
            {
                ConnectToServer();
                string mensaje = "6/" + usuario + "/" + estado;
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);
                Debug.WriteLine("Estado enviado correctamente: " + estado);
                Thread.Sleep(100);
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al enviar el estado: " + ex.Message);
                return -1;
            }
        }

        private void ChatPanel(bool visible, string mensaje)
        {
            try
            {
                // Si hay un nuevo mensaje, añadirlo al historial
                if (!string.IsNullOrEmpty(mensaje))
                {
                    string[] partes = mensaje.Split('/');
                    if (partes.Length >= 2)
                    {
                        string nombre = partes[0];
                        string mensajeChat = partes[1];
                        historialChat.Add($"{nombre}/{mensajeChat}");
                    }
                    else
                    {
                        // Mensaje de sistema o de otro formato
                        historialChat.Add(mensaje);
                    }
                }

                // Limpiar la interfaz anterior
                UserInterface.Active.Clear();

                // Crear el panel con altura fija para asegurar que todo sea visible
                Panel panel = new Panel(new Vector2(350, 500), PanelSkin.Simple, Anchor.CenterRight);
                panel.Padding = new Vector2(20, 20);
                panel.Visible = visible;
                UserInterface.Active.AddEntity(panel);

                // Agregar encabezado y línea horizontal
                panel.AddChild(new Header("Chat"));
                panel.AddChild(new HorizontalLine());

                // Panel con desplazamiento para mensajes
                VerticalScrollbar scrollPanel = new VerticalScrollbar(0, 10);
                panel.AddChild(scrollPanel);

                // Agregar todos los mensajes del historial al panel de desplazamiento
                foreach (string msg in historialChat)
                {
                    string[] partes = msg.Split('/');
                    RichParagraph mensajeParagraph;

                    if (partes.Length >= 2)
                    {
                        mensajeParagraph = new RichParagraph($"{partes[0]}: {partes[1]}");
                    }
                    else
                    {
                        mensajeParagraph = new RichParagraph(msg);
                    }

                    panel.AddChild(mensajeParagraph);
                }

                // Línea horizontal para separar
                panel.AddChild(new HorizontalLine());

                // Campo de texto con altura definida
                TextInput text = new TextInput(false);
                text.PlaceholderText = "Escribe un mensaje...";
                text.Size = new Vector2(0, 40);
                panel.AddChild(text);

                // Espacio adicional
                panel.AddChild(new LineSpace(10));

                // Panel de botones
                Panel botonesPanel = new Panel(new Vector2(0, 60), PanelSkin.None, Anchor.BottomCenter);
                botonesPanel.Padding = new Vector2(10, 10);
                panel.AddChild(botonesPanel);

                // Botón enviar
                Button enviarBtn = new Button("Enviar", ButtonSkin.Default);
                enviarBtn.Size = new Vector2(150, 35);
                enviarBtn.OnClick = (Entity btn) =>
                {
                    string mensajeEnviar = text.Value;
                    if (!string.IsNullOrEmpty(mensajeEnviar))
                    {
                        try
                        {
                            // Verificar si hay conexión antes de enviar
                            if (server != null && server.Connected && conectado)
                            {
                                string mensajeFormato = "11/" + usuario + "/" + mensajeEnviar;
                                byte[] msg = Encoding.ASCII.GetBytes(mensajeFormato);
                                server.Send(msg);

                                // Recargar el panel para mostrar el mensaje nuevo
                                ChatPanel(true, null);

                                Debug.WriteLine("[CHAT] Mensaje enviado: " + mensajeEnviar);
                            }
                            else
                            {
                                // No hay conexión, intentar reconectar
                                Debug.WriteLine("[CHAT] No hay conexión. Intentando reconectar...");
                                historialChat.Add("Sistema/Reconectando al servidor...");
                                ChatPanel(true, null);

                                // Intentar reconectar
                                ConnectToServerIfNeeded();

                                if (server != null && server.Connected)
                                {
                                    // Reintento después de reconexión
                                    string mensajeFormato = "11/" + usuario + "/" + mensajeEnviar;
                                    byte[] msg = Encoding.ASCII.GetBytes(mensajeFormato);
                                    server.Send(msg);
                                    text.Value = "";
                                    historialChat.Add("Sistema/Mensaje enviado después de reconectar");
                                    ChatPanel(true, null);
                                }
                                else
                                {
                                    historialChat.Add("Sistema/ERROR: No se pudo conectar al servidor");
                                    ChatPanel(true, null);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("[ERROR] Error al enviar mensaje: " + ex.Message);
                            historialChat.Add("Sistema/ERROR: " + ex.Message);
                            ChatPanel(true, null);
                        }
                    }
                };
                botonesPanel.AddChild(enviarBtn);

                // Botón para solicitar nuevas cartas
                Button cartasBtn = new Button("Pedir Cartas", ButtonSkin.Default);
                cartasBtn.Size = new Vector2(150, 35);
                cartasBtn.OnClick = (Entity btn) =>
                {
                    try
                    {
                        if (conectado || ConnectToServerIfNeeded())
                        {
                            GetCards(usuario);
                            historialChat.Add("Sistema/Solicitando nuevas cartas...");
                            ChatPanel(true, null);
                            Debug.WriteLine("[CARTAS] Solicitud manual de cartas enviada");
                        }
                        else
                        {
                            historialChat.Add("Sistema/ERROR: No hay conexión al servidor");
                            ChatPanel(true, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ERROR] Error al solicitar cartas: {ex.Message}");
                        historialChat.Add("Sistema/ERROR: " + ex.Message);
                        ChatPanel(true, null);
                    }
                };
                botonesPanel.AddChild(cartasBtn);

                Debug.WriteLine("[CHAT] Panel de chat inicializado con " + historialChat.Count + " mensajes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en ChatPanel: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ConnectToServer()
        {
            if (server == null || !server.Connected)
            {
                try
                {
                    IPAddress direc = IPAddress.Parse("84.235.233.248");
                    IPEndPoint ipep = new IPEndPoint(direc, 50756);
                    server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    server.Connect(ipep);
                    conectado = true;
                    Debug.WriteLine("Conexión al servidor establecida.");
                }
                catch (SocketException ex)
                {
                    Debug.WriteLine("Error al conectar al servidor: " + ex.Message);
                    conectado = false;
                    throw new Exception("No se pudo conectar al servidor.");
                }
            }
        }

        private void DisconnectFromServer()
        {
            try
            {
                Debug.WriteLine("Desconectando del servidor...");
                if (server != null && server.Connected)
                {
                    int estado = this.estado(usuario, "0");
                    server.Shutdown(SocketShutdown.Send);
                    server.Close();
                }
                conectado = false;
                stopMessageListener = true;
                Debug.WriteLine("Desconexión del servidor completada.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al desconectar del servidor: " + ex.Message);
            }
        }

        private void ReconnectToServer()
        {
            if (isReconnecting)
            {
                Debug.WriteLine("Ya se está intentando reconectar. Ignorando llamada.");
                return;
            }

            isReconnecting = true;
            Debug.WriteLine("Intentando reconectar al servidor...");
            try
            {
                DisconnectFromServer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al desconectar del servidor durante la reconexión: " + ex.Message);
            }

            int intentos = 5;
            for (int i = 0; i < intentos; i++)
            {
                try
                {
                    Debug.WriteLine($"Intentando reconectar (intento {i + 1}/{intentos})...");
                    ConnectToServer();
                    conectado = true;
                    Debug.WriteLine("Reconexión exitosa.");

                    stopMessageListener = false;
                    StartMessageListener();
                    isReconnecting = false;

                    int estado = this.estado(usuario, "1");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error al intentar reconectar (intento {i + 1}/{intentos}): {ex.Message}");
                    Thread.Sleep(2000);
                }
            }

            Debug.WriteLine("No se pudo reconectar al servidor después de varios intentos.");
            conectado = false;
            isReconnecting = false;
        }

        private void StartMessageListener()
        {
            stopMessageListener = false;
            Debug.WriteLine($"Iniciando hilo de escucha. Conectado: {conectado}, Socket válido: {(server != null && server.Connected)}");

            messageListenerThread = new Thread(() =>
            {
                try
                {
                    while (conectado && !stopMessageListener)
                    {
                        try
                        {
                            if (server != null && server.Connected)
                            {
                                byte[] buffer = new byte[1024];
                                int bytesReceived = 0;

                                try
                                {
                                    bytesReceived = server.Receive(buffer);
                                }
                                catch (SocketException se)
                                {
                                    if (se.SocketErrorCode == SocketError.TimedOut)
                                    {
                                        continue; // Continuar si hay timeout
                                    }
                                    throw;
                                }

                                if (bytesReceived > 0)
                                {
                                    string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                                    Debug.WriteLine($"[RED] Mensaje recibido: {message}");

                                    // Invocar en el hilo principal
                                    // Directly process the message; ensure thread safety if accessing UI/game state
                                    ProcessServerMessage(message);
                                }
                            }
                            else
                            {
                                Thread.Sleep(1000); // Evitar CPU al 100%
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ERROR] Error en hilo de mensajes: {ex.Message}");
                            Thread.Sleep(1000);
                        }
                    }
                }
                finally
                {
                    Debug.WriteLine("[RED] Hilo de mensajes finalizado");
                }
            });

            messageListenerThread.IsBackground = true;
            messageListenerThread.Start();
        }

        public override void Update(GameTime gameTime)
        {
            try
            {
                // Eliminar la solicitud periódica de cartas
                // tiempoDesdeUltimaSolicitud += gameTime.ElapsedGameTime;
                // if (tiempoDesdeUltimaSolicitud >= intervaloSolicitudCartas)
                // {
                //     if (conectado || ConnectToServerIfNeeded())
                //     {
                //         GetCards(usuario);
                //         tiempoDesdeUltimaSolicitud = TimeSpan.Zero;
                //     }
                // }

                // Procesar mensajes pendientes primero
                List<string> mensajesAhora = null;
                lock (mensajesLock)
                {
                    if (hayNuevosMensajes && mensajesPendientes.Count > 0)
                    {
                        mensajesAhora = new List<string>(mensajesPendientes);
                        mensajesPendientes.Clear();
                        hayNuevosMensajes = false;
                        Debug.WriteLine($"[UPDATE] Procesando {mensajesAhora.Count} mensajes pendientes");
                    }
                }

                if (mensajesAhora != null)
                {
                    foreach (string msg in mensajesAhora)
                    {
                        Debug.WriteLine($"[UPDATE] Procesando mensaje: {msg}");
                        if (msg.StartsWith("CARDS/"))
                        {
                            ProcesarCartasDelServidor(msg);
                            // Forzar la actualización de la UI después de procesar las cartas
                            _mostrarTodas = true;
                        }
                    }
                }

                // Procesar input
                KeyboardState estadoTeclado = Keyboard.GetState();

                if (estadoTeclado.IsKeyDown(Keys.Space) && _estadoTecladoAnterior.IsKeyUp(Keys.Space))
                {
                    if (_mostrarTodas)
                    {
                        if (_cartasDisponibles.Count > 0)
                        {
                            int randomIndex = _random.Next(0, _cartasDisponibles.Count);
                            _cartaSeleccionada = _cartasDisponibles[randomIndex];
                            _mostrarTodas = false;
                            Debug.WriteLine($"Carta seleccionada: índice {randomIndex}");
                        }
                    }
                    else
                    {
                        _mostrarTodas = true;
                        Debug.WriteLine("Volviendo a mostrar todas las cartas");
                    }
                }
                var keyboardState = KeyboardExtended.GetState();

                // Detectar pulsación de Escape
                if (keyboardState.WasKeyReleased(Keys.Escape))
                {
                    Debug.WriteLine("Mostrando menú Escape");
                    EscMenu(usuario, true);
                }

                // Actualizar la interfaz de usuario
                if (UserInterface.Active != null)
                {
                    UserInterface.Active.Update(gameTime);
                }

                _estadoTecladoAnterior = keyboardState;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en Update: {ex.Message}");
            }
        }

        public override void Draw(GameTime gameTime)
        {
            try
            {
                GraphicsDevice.Clear(Color.CornflowerBlue); // Color claro para ver mejor

                if (_spriteBatch == null)
                {
                    _spriteBatch = new SpriteBatch(GraphicsDevice);
                }

                _spriteBatch.Begin();

                // Mostrar información de depuración
                Debug.WriteLine($"[Draw] Cartas disponibles: {_cartasDisponibles.Count}");

                // Dibujar cartas
                if (_mostrarTodas && _cartasDisponibles != null)
                {
                    for (int i = 0; i < _cartasDisponibles.Count; i++)
                    {
                        if (_cartasDisponibles[i] != null)
                        {
                            int x = 50 + (i * 180);
                            int y = 100;

                            _spriteBatch.Draw(
                                _cartasDisponibles[i],
                                new Rectangle(x, y, 150, 225),
                                Color.White
                            );
                        }
                    }
                }
                else if (_cartaSeleccionada != null)
                {
                    int centerX = GraphicsDevice.Viewport.Width / 2 - 75;
                    int centerY = GraphicsDevice.Viewport.Height / 2 - 112;

                    _spriteBatch.Draw(
                        _cartaSeleccionada,
                        new Rectangle(centerX, centerY, 150, 225),
                        Color.White
                    );
                }

                _spriteBatch.End();

                // Dibujar la interfaz de usuario
                if (UserInterface.Active != null)
                {
                    UserInterface.Active.Draw(_spriteBatch);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Draw] Error: {ex.Message}");
            }
        }

        // Método auxiliar para crear una textura plana
        private Texture2D GetOrCreatePlainTexture(Color color)
        {
            Texture2D texture = new Texture2D(GraphicsDevice, 1, 1);
            texture.SetData(new[] { color });
            return texture;
        }

        public void SetExistingSocket(Socket existingSocket)
        {
            this.server = existingSocket;
            this.conectado = true;
        }
    }
}