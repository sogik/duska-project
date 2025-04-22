using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Screens.Transitions;
using GeonBit.UI.Entities; // Add this for Button and related UI elements
using GeonBit.UI; // Add this if GeonBit.UI is used for UserInterface
using GeonBit.UI.Utils; // Add this for UserInterface
using System.Diagnostics;
using System.Net;

namespace Duska.Screens
{
    public class GameCardScreen : GameScreen
    {
        // Texturas de cartas
        private Texture2D[] _cartas = new Texture2D[4];
        private Texture2D _cartaSeleccionada;

        // Estados del juego
        private bool _mostrarTodas = true;
        private Random _random = new Random();
        private SpriteBatch spriteBatch;
        private BitmapFont _fuente;

        // Control de entrada
        private KeyboardState _estadoTecladoAnterior;

        // Cartas disponibles
        private List<Texture2D> _cartasDisponibles = new List<Texture2D>();
        private Texture2D _background;

        private BuiltinThemes _currTheme;

        // Conexión al servidor
        private Socket server;
        private Thread atender;
        private bool isReconnecting = false;
        private volatile bool stopMessageListener = false;
        private bool conectado = false; // Indica si el cliente está conectado al servidor
        private Thread messageListenerThread;


        public string usuario;

        public GameCardScreen(Game game, string usuario) : base(game)
        {
            this.usuario = usuario;
            game.IsMouseVisible = false;
        }

        public override void LoadContent()
        {
            base.LoadContent();

            // Limpiar la interfaz de usuario antes de inicializar
            UserInterface.Active.Clear();

            // Inicializar UserInterface si no está inicializado
            if (UserInterface.Active == null)
            {
                InitializeThemeAndUI(BuiltinThemes.hd);
            }

            spriteBatch = new SpriteBatch(GraphicsDevice);
            _background = Content.Load<Texture2D>("bg");

            // Solo iniciar conexión si no hay socket
            if (server == null || !conectado)
            {
                int estado = this.estado(usuario, "1");
                ConnectToServer();
                StartMessageListener();
            }

            // Cargar cartas desde Content
            _cartas[0] = Content.Load<Texture2D>("ace");
            _cartas[1] = Content.Load<Texture2D>("jack");
            _cartas[2] = Content.Load<Texture2D>("king");
            _cartas[3] = Content.Load<Texture2D>("queen");

            // Cargar fuente
            _fuente = Content.Load<BitmapFont>("kenney-rocket-square");

            // Conectar al servidor y obtener cartas
            StartMessageListener();
        }

        private void InitializeThemeAndUI(BuiltinThemes theme)
        {
            // store current theme
            _currTheme = theme;

            // create and init the UI manager
            UserInterface.Initialize(Content, theme);

            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = spriteBatch ?? new SpriteBatch(GraphicsDevice);

            // init ui and examples
            GetCards(usuario); // Get cards from server
        }

        private void Options(bool visible)
        {
            // create panel and add to list of panels and manager
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
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // add title and text
            panel.AddChild(new Header("Pause Menu"));
            panel.AddChild(new HorizontalLine());

            // add default buttons
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
                DisconnectFromServer(); // Desconectar del servidor
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
                Game.Exit();
            };
            panel.AddChild(ExitBtn);
        }

        private void ProcesarCartasDelServidor(string cartasString)
        {
            // Formato esperado: "cards/2/1/1/2"
            string[] partes = cartasString.Split('/');
            if (partes.Length != 4)
            {
                // Si el formato es incorrecto, usar valores por defecto
                partes = new string[] { "1", "1", "1", "1" };
            }

            _cartasDisponibles.Clear();

            // Procesar cada tipo de carta
            int[] cantidades = new int[3];
            for (int i = 0; i < 3; i++)
            {
                if (int.TryParse(partes[i + 1], out int cantidad))
                {
                    cantidades[i] = cantidad;
                }
                else
                {
                    cantidades[i] = 1; // Valor por defecto si no se puede parsear
                }
            }

            // Añadir las cartas según las cantidades
            for (int i = 0; i < cantidades[0]; i++) // Aces
                _cartasDisponibles.Add(_cartas[0]);
            for (int i = 0; i < cantidades[1]; i++) // Jacks
                _cartasDisponibles.Add(_cartas[1]);
            for (int i = 0; i < cantidades[2]; i++) // Kings
                _cartasDisponibles.Add(_cartas[2]);
            for (int i = 0; i < cantidades[3]; i++) // Queens
                _cartasDisponibles.Add(_cartas[3]);
        }

        private int GetCards(string usuario)
        {
            try
            {
                ConnectToServer(); // Conectar al servidor si no está conectado

                string mensaje = "9/" + usuario + "/" + "";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine("Solicitud de cartas enviada correctamente: " + mensaje);

                // Agregar un pequeño retraso para garantizar que el mensaje se envíe
                Thread.Sleep(100);

                return 0; // Estado enviado correctamente
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al enviar el estado: " + ex.Message);
                return -1; // Error de conexión
            }
        }

        private int estado(string usuario, string estado)
        {
            try
            {
                ConnectToServer(); // Conectar al servidor si no está conectado

                string mensaje = "6/" + usuario + "/" + estado;
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine("Estado enviado correctamente: " + estado);

                // Agregar un pequeño retraso para garantizar que el mensaje se envíe
                Thread.Sleep(100);

                return 0; // Estado enviado correctamente
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al enviar el estado: " + ex.Message);
                return -1; // Error de conexión
            }
        }

        private void ConnectToServer()
        {
            if (server == null || !server.Connected)
            {
                try
                {
                    IPAddress direc = IPAddress.Parse("84.235.233.248"); // Dirección del servidor
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
                    int estado = this.estado(usuario, "0"); // Enviar estado al servidor
                    server.Shutdown(SocketShutdown.Send);
                    server.Close();
                }
                conectado = false;
                stopMessageListener = true; // Detener el hilo de mensajes
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

            int intentos = 5; // Número máximo de intentos de reconexión
            for (int i = 0; i < intentos; i++)
            {
                try
                {
                    Debug.WriteLine($"Intentando reconectar (intento {i + 1}/{intentos})...");
                    ConnectToServer();
                    conectado = true; // Actualizar la bandera
                    Debug.WriteLine("Reconexión exitosa.");

                    stopMessageListener = false; // Reiniciar el indicador para el hilo de mensajes
                    StartMessageListener(); // Reiniciar el hilo de mensajes
                    isReconnecting = false;

                    int estado = this.estado(usuario, "1"); // Actualizar el estado del usuario
                    return; // Salir del método si la reconexión es exitosa
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error al intentar reconectar (intento {i + 1}/{intentos}): {ex.Message}");
                    Thread.Sleep(2000); // Esperar 2 segundos antes de intentar nuevamente
                }
            }

            Debug.WriteLine("No se pudo reconectar al servidor después de varios intentos.");
            conectado = false;
            isReconnecting = false;
        }

        private void ProcessServerMessage(string message)
        {
            try
            {
                // Verificar si el mensaje no está vacío
                if (string.IsNullOrEmpty(message))
                {
                    Debug.WriteLine("Mensaje vacío recibido del servidor.");
                    return;
                }

                Debug.WriteLine($"Mensaje recibido: '{message}'");

                // Analizar el tipo de mensaje
                if (message.StartsWith("CARDS/"))
                {
                    string cards = message.Substring(6);
                    Debug.WriteLine("Cartas recibidas: " + cards);

                    ProcesarCartasDelServidor(cards);
                }
                else if (message.StartsWith("ERROR/"))
                {
                    // Mensaje de error
                    string errorMessage = message.Substring(6);
                    Debug.WriteLine("Error recibido del servidor: " + errorMessage);
                }
                else
                {
                    // Mensaje desconocido
                    Debug.WriteLine("Mensaje desconocido recibido: " + message);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al procesar el mensaje del servidor: " + ex.Message);
            }
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
                            byte[] buffer = new byte[1024];
                            int bytesReceived = server.Receive(buffer);

                            if (bytesReceived > 0)
                            {
                                string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                                Debug.WriteLine("Mensaje recibido del servidor: " + message);
                                ProcessServerMessage(message);
                            }
                        }
                        catch (SocketException ex)
                        {
                            Debug.WriteLine("Error en el hilo de mensajes (SocketException): " + ex.Message);
                            conectado = false; // Actualizar la bandera
                            ReconnectToServer();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error general en el hilo de mensajes: " + ex.Message);
                        }
                    }
                }
                finally
                {
                    Debug.WriteLine("El hilo de mensajes se ha detenido.");
                }
            });

            messageListenerThread.IsBackground = true;
            messageListenerThread.Start();
        }

        public override void Update(GameTime gameTime)
        {
            var estadoTeclado = Keyboard.GetState();

            // Detección de presión de ESPACIO
            if (estadoTeclado.IsKeyDown(Keys.Space) && _estadoTecladoAnterior.IsKeyUp(Keys.Space))
            {
                if (_mostrarTodas)
                {
                    // Seleccionar carta aleatoria de las disponibles
                    if (_cartasDisponibles.Count > 0)
                    {
                        int randomIndex = _random.Next(0, _cartasDisponibles.Count);
                        _cartaSeleccionada = _cartasDisponibles[randomIndex];
                        _mostrarTodas = false;
                    }
                }
                else
                {
                    // Volver a mostrar todas
                    _mostrarTodas = true;
                }
            }

            // Salir con ESC
            if (estadoTeclado.IsKeyDown(Keys.Escape))
            {
                EscMenu(usuario, true);
            }

            _estadoTecladoAnterior = estadoTeclado;

            // Actualizar la interfaz de usuario
            UserInterface.Active.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(30, 30, 60)); // Fondo oscuro

            spriteBatch.Begin();

            spriteBatch.Draw(_background, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);

            if (_mostrarTodas)
            {
                // Calcular la disposición en grid para cualquier número de cartas
                int cartasPorFila = (int)Math.Ceiling(Math.Sqrt(_cartasDisponibles.Count));
                for (int i = 0; i < _cartasDisponibles.Count; i++)
                {
                    int row = i / cartasPorFila;
                    int col = i % cartasPorFila;

                    int x = col * 200 + GraphicsDevice.Viewport.Width / 2 - (cartasPorFila * 100);
                    int y = row * 250 + GraphicsDevice.Viewport.Height / 2 - 200;

                    spriteBatch.Draw(_cartasDisponibles[i], new Rectangle(x, y, 150, 225), Color.White);
                }

                // Instrucción
                string texto = "Presiona ESPACIO para seleccionar";
                var tamano = _fuente.MeasureString(texto);
                spriteBatch.DrawString(_fuente, texto,
                    new Vector2(GraphicsDevice.Viewport.Width / 2 - tamano.Width / 2, 50),
                    Color.Gold);
            }
            else
            {
                // Dibujar carta seleccionada centrada
                spriteBatch.Draw(_cartaSeleccionada,
                    new Rectangle(
                        GraphicsDevice.Viewport.Width / 2 - 150,
                        GraphicsDevice.Viewport.Height / 2 - 225,
                        300,
                        450),
                    Color.White);

                // Instrucción
                string texto = "Presiona ESPACIO para volver";
                var tamano = _fuente.MeasureString(texto);
                spriteBatch.DrawString(_fuente, texto,
                    new Vector2(GraphicsDevice.Viewport.Width / 2 - tamano.Width / 2, 50),
                    Color.Gold);
            }

            spriteBatch.End();
        }
        public void SetExistingSocket(Socket existingSocket)
        {
            this.server = existingSocket;
            this.conectado = true;
            StartMessageListener(); // Iniciar el hilo de escucha inmediatamente
        }
    }
}