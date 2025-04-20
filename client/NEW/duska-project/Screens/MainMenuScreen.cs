using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.Screens.Transitions;
using GeonBit.UI.Entities; // Add this for Button and related UI elements
using GeonBit.UI; // Add this if GeonBit.UI is used for UserInterface
using GeonBit.UI.Utils; // Add this for UserInterface
using System.Diagnostics;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections.Generic;


namespace Duska.Screens
{
    public class MainMenuScreen : GameScreen
    {
        private SpriteBatch _spriteBatch;
        private Texture2D _background;
        private SpriteBatch spriteBatch;

        private BuiltinThemes _currTheme;

        private Socket server;
        private Thread atender;
        private bool isReconnecting = false;
        private volatile bool stopMessageListener = false;
        private Thread messageListenerThread;

        public string usuario;
        private bool conectado = false; // Indica si el cliente está conectado al servidor

        public MainMenuScreen(Game game, string usuario)
            : base(game)
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

            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _background = Content.Load<Texture2D>("bg");

            // Solo iniciar conexión si no hay socket
            if (server == null || !conectado)
            {
                int estado = this.estado(usuario, "1");
                ConnectToServer();
                StartMessageListener();
            }

            Menu(true);
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
            Menu(true);
        }

        private void Menu(bool visible)
        {
            Debug.WriteLine("MainMenu");
            int estado = this.estado(usuario, "1");
            ConnectToServer();
            StartMessageListener();

            int friends = this.friends();

            // create top panel
            int topPanelHeight = 65;
            Panel topPanel = new Panel(new Vector2(0, topPanelHeight + 2), PanelSkin.Simple, Anchor.TopCenter);
            topPanel.Padding = Vector2.Zero;
            UserInterface.Active.AddEntity(topPanel);
            topPanel.Visible = visible;

            Button playBtn = new Button("Play", ButtonSkin.Default, Anchor.Auto, new Vector2(300, topPanelHeight));
            playBtn.OnClick = (Entity btn) => { topPanel.Visible = false; UserInterface.Active.Clear(); ScreenManager.LoadScreen(new PongGameScreen(Game, usuario), new FadeTransition(GraphicsDevice, Color.Black, 0.5f)); };
            topPanel.AddChild(playBtn);

            Button listfriendsBtn = new Button("Friends", ButtonSkin.Default, Anchor.TopRight, new Vector2(300, topPanelHeight));
            listfriendsBtn.OnClick = (Entity btn) =>
            {
                friends = this.friends();
            };
            topPanel.AddChild(listfriendsBtn);
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
                Menu(true); // Regresar al menú principal
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
                Menu(true);
            };
            panel.AddChild(resumeBtn);

            Button optionsBtn = new Button("Options", ButtonSkin.Default);
            optionsBtn.OnClick = (Entity btn) =>
            {
                Options(true);
                panel.Visible = false;
            };
            panel.AddChild(optionsBtn);

            Button miscBtn = new Button("Extras", ButtonSkin.Default);
            miscBtn.OnClick = (Entity btn) =>
            {
                Extras(true);
                panel.Visible = false;
            };
            panel.AddChild(miscBtn);

            Button ExitBtn = new Button("Exit", ButtonSkin.Default);
            ExitBtn.OnClick = (Entity btn) =>
            {
                int estado = this.estado(usuario, "0");
                Debug.WriteLine("Estado enviado: " + estado);
                DisconnectFromServer(); // Desconectar del servidor
                panel.Visible = false;
                Game.Exit();
            };
            panel.AddChild(ExitBtn);
        }

        private void Extras(bool visible)
        {
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // add title and text
            panel.AddChild(new Header("Extras"));
            panel.AddChild(new HorizontalLine());

            // add default buttons
            Button listarUBtn = new Button("Usuarios", ButtonSkin.Default);
            listarUBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                ListaUsuarios(); // Mostrar la lista de usuarios
            };
            panel.AddChild(listarUBtn);

            Button listarPBtn = new Button("Partidas", ButtonSkin.Default);
            listarPBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                ListaPartidas(); // Mostrar la lista de partidas
            };
            panel.AddChild(listarPBtn);

            Button listarPGBtn = new Button("Partidas Ganadas", ButtonSkin.Default);
            listarPGBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                ListaPartidasGanadas(); // Mostrar la lista de partidas ganadas
            };
            panel.AddChild(listarPGBtn);

            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                EscMenu(usuario, true); // Regresar al menú principal
            };
            panel.AddChild(backBtn);
        }

        private void ListaUsuariosPanel(bool visible, string usuarios)
        {
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // add title and text
            panel.AddChild(new Header("Lista de Usuarios"));
            panel.AddChild(new HorizontalLine());

            SelectList list = new SelectList(new Vector2(0, 280)) { Identifier = "UsuariosList" };
            panel.AddChild(list);

            // Agregar un botón para regresar al menú principal
            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                Extras(true); // Regresar al menú principal
            };
            panel.AddChild(backBtn);

            // Actualizar la lista de amigos
            SelectList usuariosList = panel.Find<SelectList>("UsuariosList");
            if (usuariosList != null)
            {
                usuariosList.ClearItems();
                string[] friendsArray = usuarios.Split('/');
                foreach (string friend in friendsArray)
                {
                    usuariosList.AddItem(friend);
                }
            }

            panel.Visible = visible;
        }

        private void ListaPartidasPanel(bool visible, string partidas)
        {
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // add title and text
            panel.AddChild(new Header("Lista de Partidas"));
            panel.AddChild(new HorizontalLine());

            SelectList list = new SelectList(new Vector2(0, 280)) { Identifier = "PartidasList" };
            panel.AddChild(list);

            // Agregar un botón para regresar al menú principal
            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                Extras(true); // Regresar al menú principal
            };
            panel.AddChild(backBtn);

            SelectList partidasList = panel.Find<SelectList>("PartidasList");
            if (partidasList != null)
            {
                partidasList.ClearItems();
                string[] friendsArray = partidas.Split('/');
                foreach (string friend in friendsArray)
                {
                    partidasList.AddItem(friend);
                }
            }

            panel.Visible = visible;
        }

        private void ListaPartidasGanadasPanel(bool visible, string partidasGanadas)
        {
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // add title and text
            panel.AddChild(new Header("Lista de Partidas Ganadas"));
            panel.AddChild(new HorizontalLine());

            SelectList list = new SelectList(new Vector2(0, 280)) { Identifier = "partidasGanadasList" };
            panel.AddChild(list);

            // Agregar un botón para regresar al menú principal
            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                Extras(true); // Regresar al menú principal
            };
            panel.AddChild(backBtn);

            SelectList partidasGanadasList = panel.Find<SelectList>("partidasGanadasList");
            if (partidasGanadasList != null)
            {
                partidasGanadasList.ClearItems();
                string[] friendsArray = partidasGanadas.Split('/');
                foreach (string friend in friendsArray)
                {
                    partidasGanadasList.AddItem(friend);
                }
            }

            panel.Visible = visible;
        }

        private int ListaUsuarios()
        {
            try
            {
                if (!conectado)
                {
                    ConnectToServer(); // Conectar al servidor si no está conectado
                }

                // Mostrar un panel de carga mientras se espera la respuesta
                ListaUsuariosPanel(true, "Cargando...");

                string mensaje = "2/brr/brr";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine("Petición de lista de usuarios enviada correctamente: " + mensaje);
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al enviar la petición de usuarios: " + ex.Message);
                conectado = false; // Actualizar la bandera
                return -1;
            }
        }

        private int ListaPartidas()
        {
            try
            {
                if (!conectado)
                {
                    ConnectToServer(); // Conectar al servidor si no está conectado
                }

                // Mostrar un panel de carga mientras se espera la respuesta
                ListaPartidasPanel(true, "Cargando...");

                string mensaje = "3/brr/brr";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine("Petición de lista de partidas enviada correctamente: " + mensaje);
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al enviar la petición de partidas: " + ex.Message);
                conectado = false; // Actualizar la bandera
                return -1;
            }
        }

        private int ListaPartidasGanadas()
        {
            try
            {
                if (!conectado)
                {
                    ConnectToServer(); // Conectar al servidor si no está conectado
                }

                // Mostrar un panel de carga mientras se espera la respuesta
                ListaPartidasGanadasPanel(true, "Cargando...");

                string mensaje = "4/" + usuario + "/brr";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine("Petición de lista de partidas ganadas enviada correctamente: " + mensaje);
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al enviar la petición de partidas ganadas: " + ex.Message);
                conectado = false; // Actualizar la bandera
                return -1;
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
                    conectado = true; // Actualizar la bandera
                    Debug.WriteLine("Conexión al servidor establecida.");
                }
                catch (SocketException ex)
                {
                    Debug.WriteLine("Error al conectar al servidor: " + ex.Message);
                    conectado = false; // Actualizar la bandera
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
                conectado = false; // Actualizar la bandera
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
            conectado = false; // Actualizar la bandera
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
                if (message.StartsWith("LIST/"))
                {
                    // Mensaje de lista de amigos
                    string friends = message.Substring(5); // Eliminar el prefijo "LIST/"
                    Debug.WriteLine("Lista de amigos recibida: " + friends);

                    // Actualizar la lista de amigos
                    FriendsListPanel(true, friends);

                }
                else if (message.StartsWith("LISTU/"))
                {
                    // Mensaje de lista de usuarios
                    string usuarios = message.Substring(6); // Eliminar el prefijo "LISTU/"
                    Debug.WriteLine("Lista de usuarios recibida: " + usuarios);

                    // Actualizar la lista de usuarios
                    ListaUsuariosPanel(true, usuarios);
                }
                else if (message.StartsWith("LISTP/"))
                {
                    // Mensaje de lista de partidas
                    string partidas = message.Substring(6); // Eliminar el prefijo "LISTP/"
                    Debug.WriteLine("Lista de partidas recibida: " + partidas);

                    // Actualizar la lista de partidas
                    ListaPartidasPanel(true, partidas);
                }
                else if (message.StartsWith("LISTPG/"))
                {
                    // Mensaje de lista de partidas ganadas
                    string partidasGanadas = message.Substring(8); // Eliminar el prefijo "LISTPG/"
                    Debug.WriteLine("Lista de partidas ganadas recibida: " + partidasGanadas);

                    // Actualizar la lista de partidas ganadas
                    ListaPartidasGanadasPanel(true, partidasGanadas);
                }
                else if (message.StartsWith("ERROR/"))
                {
                    // Mensaje de error
                    string errorMessage = message.Substring(6); // Eliminar el prefijo "ERROR/"
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

        private int friends()
        {
            try
            {
                if (!conectado)
                {
                    ConnectToServer(); // Conectar al servidor si no está conectado
                }

                string mensaje = "5/brr/brr";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine("Petición enviada correctamente: " + mensaje);
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al enviar la petición de amigos: " + ex.Message);
                conectado = false; // Actualizar la bandera
                return -1;
            }
        }

        private void FriendsListPanel(bool visible, string friends)
        {
            // Crear el panel
            Panel panel = new Panel(new Vector2(300, -1), PanelSkin.Simple, Anchor.CenterRight);
            panel.Padding = Vector2.Zero;
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // Agregar encabezado y línea horizontal
            panel.AddChild(new Header("Friends"));
            panel.AddChild(new HorizontalLine());

            // Crear la lista de amigos
            SelectList list = new SelectList(new Vector2(0, 280)) { Identifier = "FriendsList" };
            panel.AddChild(list);

            // Agregar botón de regreso
            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                Menu(true);
            };
            panel.AddChild(backBtn);

            // Actualizar la lista de amigos
            SelectList friendsList = panel.Find<SelectList>("FriendsList");
            if (friendsList != null)
            {
                friendsList.ClearItems();
                string[] friendsArray = friends.Split('/');
                foreach (string friend in friendsArray)
                {
                    friendsList.AddItem(friend);
                }
            }

            Debug.WriteLine("FriendList Actualizada");

            // Mostrar u ocultar el panel
            panel.Visible = visible;
        }

        public override void Update(GameTime gameTime)
        {
            var mouseState = MouseExtended.GetState();
            var keyboardState = KeyboardExtended.GetState();

            if (keyboardState.WasKeyReleased(Keys.Escape))
                EscMenu(usuario, true);

            // Actualizar la interfaz de usuario
            UserInterface.Active.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Magenta);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(_background, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
            _spriteBatch.End();

            // Dibujar la interfaz de usuario
            UserInterface.Active.Draw(_spriteBatch);
        }

        // Método para recibir el socket existente desde TitleScreen
        public void SetExistingSocket(Socket existingSocket)
        {
            this.server = existingSocket;
            this.conectado = true;
            StartMessageListener(); // Iniciar el hilo de escucha inmediatamente
        }
    }
}
