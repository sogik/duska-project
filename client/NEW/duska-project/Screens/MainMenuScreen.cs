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
        public string destinatario;

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
            playBtn.OnClick = (Entity btn) =>
            {
                topPanel.Visible = false;
                UserInterface.Active.Clear();

                Socket socketParaGame = server;
                server = null; // Evitar que se cierre en finally

                // Cambiar a la pantalla principal
                GameCardScreen gameCardScreen = new GameCardScreen(Game, usuario);
                gameCardScreen.SetExistingSocket(socketParaGame);

                UserInterface.Active.Clear();
                ScreenManager.LoadScreen(gameCardScreen, new FadeTransition(GraphicsDevice, Color.Black, 0.5f));
            };
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
                conectado = false;
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
                conectado = false;
                return -1;
            }
        }

        private void Invitacion(int tipo, string destinatarios, string mensaje)
        {
            try
            {
                if (!conectado)
                {
                    ConnectToServer(); // Conectar al servidor si no está conectado
                }
                // Verificar que no estés intentando enviarte una invitación a ti mismo
                if (destinatarios == usuario)
                {
                    Debug.WriteLine("Error: No puedes enviarte una invitación a ti mismo");
                    return;
                }

                // Eliminar saltos de línea y espacios no deseados
                //destinatarios = destinatarios.Trim();
                //mensaje = mensaje.Trim();

                if (tipo == 1)
                {
                    string mensaje2 = "7/" + usuario + "/" + destinatarios + "/" + mensaje;
                    Debug.WriteLine($"Enviando invitación: {mensaje2}");
                    byte[] msg = Encoding.ASCII.GetBytes(mensaje2);
                    server.Send(msg);
                    Debug.WriteLine($"Invitación enviada desde: {usuario} a: {destinatarios}");
                }
                else if (tipo == 2)
                {
                    string mensaje2 = "8/" + usuario + "/" + destinatarios + "/" + mensaje;
                    Debug.WriteLine($"Enviando respuesta: {mensaje2}");
                    byte[] msg = Encoding.ASCII.GetBytes(mensaje2);
                    server.Send(msg);
                    Debug.WriteLine($"Respuesta de invitación enviada desde: {usuario} a: {destinatarios}");
                }
            }
            catch (SocketException ex)
            {
                Debug.WriteLine("Error de socket al enviar invitación: " + ex.Message);
                conectado = false;
                ReconnectToServer(); // Intentar reconectar automáticamente
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error general al enviar invitación: " + ex.Message);
                conectado = false;
            }
        }

        private void InvitacionPanel(int tipo, bool visible, string mensaje)
        {
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // add title and text
            panel.AddChild(new Header("Invitación"));
            panel.AddChild(new HorizontalLine());

            string[] mensaje2 = mensaje.Split('/');

            if (tipo == 1)
            {
                // Mensaje de invitación
                panel.AddChild(new Paragraph(mensaje));
                panel.AddChild(new HorizontalLine());

                Button acceptBtn = new Button("Accept", ButtonSkin.Default);
                acceptBtn.OnClick = (Entity btn) =>
                {
                    Debug.WriteLine("Invitación aceptada.");

                    string respuesta = "Aceptada";

                    string remitente = mensaje2[0].Trim();

                    Invitacion(2, remitente, respuesta); // Enviar invitación al servidor

                    panel.Visible = false;
                };
                panel.AddChild(acceptBtn);

                Button declineBtn = new Button("Decline", ButtonSkin.Default);
                declineBtn.OnClick = (Entity btn) =>
                {
                    Debug.WriteLine("Invitación rechazada.");

                    string respuesta = "Rechazada";

                    // Asegurarnos que el nombre del remitente no tenga caracteres extraños
                    string remitente = mensaje2[0].Trim();

                    Invitacion(2, remitente, respuesta); // Enviar respuesta al servidor

                    panel.Visible = false;
                };
                panel.AddChild(declineBtn);

                panel.Visible = visible;
            }
            if (tipo == 2)
            {
                mensaje = mensaje2[0] + " ha " + mensaje2[1] + " la invitación a jugar.";
                Debug.WriteLine("Invitación aceptada o rechazada.");
                // Mensaje de invitación aceptada o rechazada
                panel.AddChild(new Paragraph(mensaje));
                panel.AddChild(new HorizontalLine());

                Button acceptBtn = new Button("Ok", ButtonSkin.Default);
                acceptBtn.OnClick = (Entity btn) =>
                {
                    panel.Visible = false;
                };
                panel.AddChild(acceptBtn);

                panel.Visible = visible;
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
            SelectList list = new SelectList(new Vector2(0, 220)) { Identifier = "FriendsList" };
            panel.AddChild(list);

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

            // Agregar un botón para interactuar con el amigo seleccionado
            Button interactBtn = new Button("Opciones del amigo", ButtonSkin.Default);
            interactBtn.OnClick = (Entity btn) =>
            {
                if (friendsList.SelectedIndex >= 0)
                {
                    string selectedFriend = friendsList.SelectedValue;
                    Debug.WriteLine($"Mostrando opciones para: {selectedFriend}");

                    // Calcular posición para el menú contextual
                    Vector2 menuPosition = new Vector2(
                        panel.GetActualDestRect().X - 160,  // Posición X a la izquierda del panel de amigos
                        panel.GetActualDestRect().Y + 150); // Posición Y centrada

                    panel.Visible = false;
                    ShowFriendOptions(selectedFriend, menuPosition);
                }
                else
                {
                    Debug.WriteLine("Ningún amigo seleccionado");
                }
            };
            panel.AddChild(interactBtn);

            // Agregar botón de regreso
            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                Menu(true);
            };
            panel.AddChild(backBtn);

            // Mostrar u ocultar el panel
            panel.Visible = visible;
        }

        // Nuevo método para mostrar opciones de amigo
        private void ShowFriendOptions(string friendName, Vector2 position)
        {
            Debug.WriteLine($"Intentando mostrar opciones para {friendName} en posición {position}");

            // Verificar que no sea el propio usuario
            if (friendName == usuario)
            {
                Debug.WriteLine("No puedes interactuar contigo mismo");
                Menu(true); // Volver al menú principal
                return;
            }

            // Eliminar cualquier menú contextual existente
            Entity existingMenu = UserInterface.Active.Root.Find("FriendOptionsMenu");
            if (existingMenu != null)
            {
                Debug.WriteLine("Eliminando menú existente");
                UserInterface.Active.RemoveEntity(existingMenu);
            }

            // Crear un panel con las opciones y asegurar que sea visible en pantalla
            Panel optionsPanel = new Panel(new Vector2(200, 150), PanelSkin.Default, Anchor.Center);
            optionsPanel.Identifier = "FriendOptionsMenu";

            // No es necesario establecer Offset cuando usamos Anchor.Center
            // El panel se centrará automáticamente

            // Asegurar que el panel sea visible
            optionsPanel.Visible = true;

            // Título
            Header header = new Header(friendName);
            optionsPanel.AddChild(header);
            optionsPanel.AddChild(new HorizontalLine());

            // Botón para invitar al amigo
            Button inviteBtn = new Button("Invitar a jugar", ButtonSkin.Default);
            inviteBtn.OnClick = (Entity btn) =>
            {
                destinatario = friendName;
                Debug.WriteLine($"Enviando invitación a {friendName}...");

                try
                {
                    Invitacion(1, destinatario, "Invitacion de juego");
                    UserInterface.Active.RemoveEntity(optionsPanel);
                    Menu(true); // Regresar al menú principal
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error al enviar invitación: {ex.Message}");
                    UserInterface.Active.RemoveEntity(optionsPanel);
                    Menu(true); // Regresar al menú principal
                }
            };
            optionsPanel.AddChild(inviteBtn);

            // Botón para cerrar el menú
            Button closeBtn = new Button("Cerrar", ButtonSkin.Default);
            closeBtn.OnClick = (Entity btn) =>
            {
                Debug.WriteLine("Cerrando menú de opciones");
                UserInterface.Active.RemoveEntity(optionsPanel);
                Menu(true); // Regresar al menú principal
            };
            optionsPanel.AddChild(closeBtn);

            // Añadir al UI
            UserInterface.Active.AddEntity(optionsPanel);
            Debug.WriteLine($"Menú de opciones creado en posición centrada");
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
                if (message.StartsWith("LIST/"))
                {
                    // Mensaje de lista de amigos
                    string friends = message.Substring(5);
                    Debug.WriteLine("Lista de amigos recibida: " + friends);

                    // Actualizar la lista de amigos
                    FriendsListPanel(true, friends);

                }
                else if (message.StartsWith("LISTU/"))
                {
                    // Mensaje de lista de usuarios
                    string usuarios = message.Substring(6);
                    Debug.WriteLine("Lista de usuarios recibida: " + usuarios);

                    // Actualizar la lista de usuarios
                    ListaUsuariosPanel(true, usuarios);
                }
                else if (message.StartsWith("LISTP/"))
                {
                    // Mensaje de lista de partidas
                    string partidas = message.Substring(6);
                    Debug.WriteLine("Lista de partidas recibida: " + partidas);

                    // Actualizar la lista de partidas
                    ListaPartidasPanel(true, partidas);
                }
                else if (message.StartsWith("LISTPG/"))
                {
                    // Mensaje de lista de partidas ganadas
                    string partidasGanadas = message.Substring(8);
                    Debug.WriteLine("Lista de partidas ganadas recibida: " + partidasGanadas);

                    // Actualizar la lista de partidas ganadas
                    ListaPartidasGanadasPanel(true, partidasGanadas);
                }
                else if (message.StartsWith("INV/"))
                {
                    string Message = message.Substring(4);
                    Debug.WriteLine("Mensaje recibido del servidor: " + Message);

                    InvitacionPanel(1, true, Message); // Llamar a la función para enviar la invitación
                }
                else if (message.StartsWith("INVR/"))
                {
                    string Message = message.Substring(5);
                    Debug.WriteLine("Mensaje recibido del servidor: " + Message);

                    InvitacionPanel(2, true, Message);
                }
                else if (message.StartsWith("INV2/"))
                {
                    string Message = message.Substring(5);
                    Debug.WriteLine("Mensaje recibido del servidor: " + Message);

                    InvitacionPanel(2, true, Message); // Llamar a la función para enviar la invitación
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

        private MouseStateExtended? _previousMouseState;

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
