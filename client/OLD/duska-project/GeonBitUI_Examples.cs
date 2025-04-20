#region File Description
//-----------------------------------------------------------------------------
// This program show GeonBit.UI examples and usage.
//
// GeonBit.UI is an export of the UI system used for GeonBit (an open source 
// game engine in MonoGame) and is free to use under the MIT license.
//
// To learn more about GeonBit.UI, you can visit the git repo:
// https://github.com/RonenNess/GeonBit.UI
//
// Or explore the different README files scattered in the solution directory. 
//
// Author: Ronen Ness.
// Since: 2016.
//-----------------------------------------------------------------------------
#endregion

// using MonoGame and basic system stuff
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

// using GeonBit UI elements
using GeonBit.UI.Entities;
using System.Diagnostics;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;

namespace GeonBit.UI.Examples
{
    /// <summary>
    /// GeonBit.UI.Example is just an example code. Everything here is not a part of the GeonBit.UI framework, but merely an example of how to use it.
    /// </summary>
    [System.Runtime.CompilerServices.CompilerGenerated]
    class NamespaceDoc
    {
    }

    /// <summary>
    /// This is the main 'Game' instance for your game.
    /// </summary>
    public class GeonBitUI_Examples : Game
    {
        // graphics and spritebatch
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        private Socket server;
        private Thread atender;

        private Thread estadolist;

        private int actualizarlistaamigos = 0;

        private Thread messageListenerThread;

        private volatile bool stopMessageListener = false;

        string usuario;

        bool conectado = false;
        bool enjuego = false;

        private bool isReconnecting = false;

        // the main game instance
        //private MonoGame.Extended.Graphics.Texture2DRegion _ace;
        //private MonoGame.Extended.Graphics.Texture2DRegion _king;
        //private MonoGame.Extended.Graphics.Texture2DRegion _queen;
        //private MonoGame.Extended.Graphics.Texture2DRegion _joker;
        //private MonoGame.Extended.Graphics.Texture2DRegion _demon;


        // all the example panels (screens)
        List<Panel> panels = new List<Panel>();

        // buttons to rotate examples
        Button nextExampleButton;
        Button previousExampleButton;

        // paragraph that shows the currently active entity
        Paragraph targetEntityShow = null;

        // current example shown
        int currExample = 0;

        // current theme
        BuiltinThemes _currTheme;

        /// <summary>
        /// Create the game instance.
        /// </summary>
        public GeonBitUI_Examples()
        {
            // init graphics device manager and set content root
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            Window.IsBorderless = true;
        }

        /// <summary>
        /// Initialize the main application.
        /// </summary>
        protected override void Initialize()
        {
            // make the window fullscreen (but still with border and top control bar)
            int _ScreenWidth = graphics.GraphicsDevice.Adapter.CurrentDisplayMode.Width;
            int _ScreenHeight = graphics.GraphicsDevice.Adapter.CurrentDisplayMode.Height;
            graphics.PreferredBackBufferWidth = (int)_ScreenWidth;
            graphics.PreferredBackBufferHeight = (int)_ScreenHeight;
            graphics.IsFullScreen = false;
            graphics.ApplyChanges();

            // init theme and ui
            InitializeThemeAndUI(BuiltinThemes.hd);
        }

        private void InitializeThemeAndUI(BuiltinThemes theme)
        {
            // clear previous panels
            panels.Clear();

            // store current theme
            _currTheme = theme;

            // create and init the UI manager
            UserInterface.Initialize(Content, theme);
            UserInterface.Active.UseRenderTarget = true;

            // draw cursor outside the render target
            UserInterface.Active.IncludeCursorInRenderTarget = false;

            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = spriteBatch ?? new SpriteBatch(GraphicsDevice);

            // init ui and examples

            if (conectado)
            {
                // if we are connected, show the main menu
                MainMenuGame(true);
            }
            else
            {
                // if we are not connected, show the login panel
                Menu();
            }
        }

        /// <summary>
        /// Create the top bar with next / prev buttons etc, and init all UI example panels.
        /// </summary> 
        /// 

        public void Menu()
        {
            Panel panel = new Panel(new Vector2(450, -1));
            panels.Add(panel);
            UserInterface.Active.AddEntity(panel);

            // add title and text
            panel.AddChild(new Header("Main Menu"));
            panel.AddChild(new HorizontalLine());

            // add default buttons
            Button loginBtn = new Button("Login", ButtonSkin.Default);
            loginBtn.OnClick = (Entity btn) =>
            {
                LoginPanel(true);
                panel.Visible = false;
            };
            panel.AddChild(loginBtn);

            Button optionsBtn = new Button("Options", ButtonSkin.Default);
            optionsBtn.OnClick = (Entity btn) =>
            {
                Options(true);
                panel.Visible = false;
            };
            panel.AddChild(optionsBtn);

            Button ExitBtn = new Button("Exit", ButtonSkin.Default);
            ExitBtn.OnClick = (Entity btn) =>
            {
                Debug.WriteLine("Saliendo del juego...");
                DisconnectFromServer(); // Desconectar del servidor
                Exit(); // Cerrar el juego
            };
            panel.AddChild(ExitBtn);
        }

        protected void InitExamplesAndUI()
        {
            // will init examples only if true
            bool initExamples = true;

            // create top panel
            int topPanelHeight = 65;
            Panel topPanel = new Panel(new Vector2(0, topPanelHeight + 2), PanelSkin.Simple, Anchor.TopCenter);
            topPanel.Padding = Vector2.Zero;
            UserInterface.Active.AddEntity(topPanel);
            topPanel.Visible = false;

            // add previous example button
            previousExampleButton = new Button("<- Back", ButtonSkin.Default, Anchor.Auto, new Vector2(300, topPanelHeight));
            previousExampleButton.OnClick = (Entity btn) => { };
            topPanel.AddChild(previousExampleButton);


            // add next example button
            nextExampleButton = new Button("Next ->", ButtonSkin.Default, Anchor.TopRight, new Vector2(300, topPanelHeight));
            nextExampleButton.OnClick = (Entity btn) => { };
            nextExampleButton.Identifier = "next_btn";
            topPanel.AddChild(nextExampleButton);

            // events panel for debug
            Panel eventsPanel = new Panel(new Vector2(400, 530), PanelSkin.Simple, Anchor.CenterLeft, new Vector2(-10, 0));
            eventsPanel.Visible = false;

            // events log (single-time events)
            eventsPanel.AddChild(new Label("Events Log:"));
            SelectList eventsLog = new SelectList(size: new Vector2(-1, 280));
            eventsLog.ExtraSpaceBetweenLines = -8;
            eventsLog.ItemsScale = 0.5f;
            eventsLog.Locked = true;
            eventsPanel.AddChild(eventsLog);

            // current events (events that happen while something is true)
            eventsPanel.AddChild(new Label("Current Events:"));
            SelectList eventsNow = new SelectList(size: new Vector2(-1, 100));
            eventsNow.ExtraSpaceBetweenLines = -8;
            eventsNow.ItemsScale = 0.5f;
            eventsNow.Locked = true;
            eventsPanel.AddChild(eventsNow);

            // paragraph to show currently active panel
            targetEntityShow = new Paragraph("test", Anchor.Auto, Color.White, scale: 0.75f);
            eventsPanel.AddChild(targetEntityShow);

            // add the events panel
            UserInterface.Active.AddEntity(eventsPanel);

            // whenever events log list size changes, make sure its not too long. if it is, trim it.
            eventsLog.OnListChange = (Entity entity) =>
            {
                SelectList list = (SelectList)entity;
                if (list.Count > 100)
                {
                    list.RemoveItem(0);
                }
            };

            // listen to all global events - one timers
            UserInterface.Active.OnClick = (Entity entity) => { eventsLog.AddItem("Click: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnRightClick = (Entity entity) => { eventsLog.AddItem("RightClick: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnMouseDown = (Entity entity) => { eventsLog.AddItem("MouseDown: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnRightMouseDown = (Entity entity) => { eventsLog.AddItem("RightMouseDown: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnMouseEnter = (Entity entity) => { eventsLog.AddItem("MouseEnter: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnMouseLeave = (Entity entity) => { eventsLog.AddItem("MouseLeave: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnMouseReleased = (Entity entity) => { eventsLog.AddItem("MouseReleased: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnMouseWheelScroll = (Entity entity) => { eventsLog.AddItem("Scroll: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnStartDrag = (Entity entity) => { eventsLog.AddItem("StartDrag: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnStopDrag = (Entity entity) => { eventsLog.AddItem("StopDrag: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnFocusChange = (Entity entity) => { eventsLog.AddItem("FocusChange: " + entity.GetType().Name); eventsLog.scrollToEnd(); };
            UserInterface.Active.OnValueChange = (Entity entity) => { if (entity.Parent == eventsLog) { return; } eventsLog.AddItem("ValueChanged: " + entity.GetType().Name); eventsLog.scrollToEnd(); };

            // clear the current events after every frame they were drawn
            eventsNow.AfterDraw = (Entity entity) => { eventsNow.ClearItems(); };

            // listen to all global events - happening now
            UserInterface.Active.WhileDragging = (Entity entity) => { eventsNow.AddItem("Dragging: " + entity.GetType().Name); eventsNow.scrollToEnd(); };
            UserInterface.Active.WhileMouseDown = (Entity entity) => { eventsNow.AddItem("MouseDown: " + entity.GetType().Name); eventsNow.scrollToEnd(); };
            UserInterface.Active.WhileMouseHover = (Entity entity) => { eventsNow.AddItem("MouseHover: " + entity.GetType().Name); eventsNow.scrollToEnd(); };
            eventsNow.MaxItems = 4;

            // add extra info button
            var offsetX = 140;
            Button infoBtn = new Button("  Events", anchor: Anchor.BottomLeft, size: new Vector2(280, -1), offset: new Vector2(offsetX, 0));
            infoBtn.Visible = false;
            offsetX += 280;
            infoBtn.AddChild(new Icon(IconType.Scroll, Anchor.CenterLeft), true);
            infoBtn.OnClick = (Entity entity) =>
            {
                eventsPanel.Visible = !eventsPanel.Visible;
            };
            infoBtn.ToggleMode = true;
            infoBtn.ToolTipText = "Show events log.";
            UserInterface.Active.AddEntity(infoBtn);

            // add button to enable debug mode
            Button debugBtn = new Button("Debug Mode", anchor: Anchor.BottomLeft, size: new Vector2(260, -1), offset: new Vector2(offsetX, 0));
            offsetX += 260;
            debugBtn.OnClick = (Entity entity) =>
            {
                UserInterface.Active.DebugDraw = !UserInterface.Active.DebugDraw;
            };
            debugBtn.ToggleMode = true;
            debugBtn.ToolTipText = "Enable special debug drawing mode.";
            UserInterface.Active.AddEntity(debugBtn);

            // init all examples

            if (initExamples)
            {

                // example: welcome message
                {
                    // create panel and add to list of panels and manager
                    Panel panel = new Panel(new Vector2(450, -1));
                    panels.Add(panel);
                    UserInterface.Active.AddEntity(panel);

                    // add title and text
                    panel.AddChild(new Header("Main Menu"));
                    panel.AddChild(new HorizontalLine());

                    // add default buttons
                    Button loginBtn = new Button("Login", ButtonSkin.Default);
                    loginBtn.OnClick = (Entity btn) =>
                    {
                        LoginPanel(true);
                        panel.Visible = false;
                    };
                    panel.AddChild(loginBtn);

                    Button optionsBtn = new Button("Options", ButtonSkin.Default);
                    optionsBtn.OnClick = (Entity btn) =>
                    {
                        Options(true);
                        panel.Visible = false;
                    };
                    panel.AddChild(optionsBtn);

                    Button ExitBtn = new Button("Exit", ButtonSkin.Default);
                    ExitBtn.OnClick = (Entity btn) =>
                    {
                        Exit();
                    };
                    panel.AddChild(ExitBtn);

                }

                // example: text input
                // init panels and buttons
                UpdateAfterExampleChange();

            }

            // once done init, clear events log
            //eventsLog.ClearItems();

            // call base initialize
            base.Initialize();
        }

        /// <summary>
        /// Show next UI example.
        /// </summary>

        private void ConnectToServer()
        {
            if (server == null || !server.Connected)
            {
                try
                {
                    IPAddress direc = IPAddress.Parse("10.4.119.5");
                    IPEndPoint ipep = new IPEndPoint(direc, 50756);
                    server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    server.Connect(ipep);
                }
                catch (SocketException ex)
                {
                    Debug.WriteLine("Error al conectar al servidor: " + ex.Message);
                    throw new Exception("No se pudo conectar al servidor.");
                }
            }
        }

        private void DisconnectFromServer()
        {
            try
            {
                Debug.WriteLine("Desconectando del servidor. Estado actual de conectado: " + conectado);

                if (conectado)
                {
                    conectado = false;
                    int estado = this.estado(usuario, "0"); // Enviar estado al servidor
                    Debug.WriteLine("Estado enviado correctamente: " + estado);
                }

                if (server != null && server.Connected)
                {
                    try
                    {
                        // Asegurarse de que todos los datos se envíen antes de cerrar
                        server.Shutdown(SocketShutdown.Send);
                        Debug.WriteLine("Socket cerrado para envío.");
                        server.Close();
                        Debug.WriteLine("Desconectado del servidor.");
                    }
                    catch (SocketException ex)
                    {
                        Debug.WriteLine("Error al desconectar del servidor: " + ex.Message);
                    }
                    finally
                    {
                        server = null;
                    }
                }

                // Detener el hilo de mensajes
                stopMessageListener = true;
                if (messageListenerThread != null && messageListenerThread.IsAlive)
                {
                    Debug.WriteLine("Esperando que el hilo de mensajes se detenga...");
                    messageListenerThread.Join(2000); // Esperar un máximo de 2 segundos
                    Debug.WriteLine("Hilo de mensajes detenido.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error en DisconnectFromServer: " + ex.Message);
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
                    Debug.WriteLine("Intentando reconectar. Estado de conectado: " + conectado);
                    ConnectToServer();
                    conectado = true; // Actualizar el estado de conexión
                    Debug.WriteLine("Reconexión exitosa. Estado de conectado: " + conectado);

                    // Restablecer el indicador para el hilo de mensajes
                    stopMessageListener = false;

                    // Reiniciar el hilo de mensajes
                    StartMessageListener();
                    Debug.WriteLine("Hilo de mensajes reiniciado.");
                    isReconnecting = false;
                    int estado = this.estado(usuario, "1");
                    return; // Salir del método si la reconexión es exitosa
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error al intentar reconectar (intento {i + 1}/{intentos}): {ex.Message}");
                    Thread.Sleep(2000); // Esperar 2 segundos antes de intentar nuevamente
                }
            }

            Debug.WriteLine("No se pudo reconectar al servidor después de varios intentos.");
            conectado = false; // Marcar como desconectado si no se pudo reconectar
            isReconnecting = false;
        }

        public int signup(string usuario1, string contrasena)
        {
            try
            {
                IPAddress direc = IPAddress.Parse("10.4.119.5");
                IPEndPoint ipep = new IPEndPoint(direc, 50756);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    server.Connect(ipep);

                    if (!server.Connected)
                    {
                        return -1; // Error de conexión
                    }

                    // Enviar datos de inicio de sesión
                    string mensaje = "0/" + usuario1 + "/" + contrasena;
                    byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);

                    // Recibir respuesta del servidor
                    byte[] msg2 = new byte[80];
                    int bytesRecibidos = server.Receive(msg2);
                    if (bytesRecibidos == 0)
                    {
                        return -1; // Error en la conexión
                    }

                    mensaje = Encoding.ASCII.GetString(msg2).Split('\0')[0];
                    if (mensaje == "0")
                    {
                        return 0; // Registro exitoso
                    }
                    else if (mensaje == "1")
                    {
                        return 1; // Credenciales incorrectas
                    }
                    else if (mensaje == "2")
                    {
                        return 2; // Usuario no existe
                    }
                    else if (mensaje == "3")
                    {
                        return 3; // Error en la conexión
                    }
                    else
                    {
                        return -1; // Error
                    }
                }
                catch (SocketException)
                {
                    return -1; // Error de conexión
                }
                finally
                {
                    if (server != null)
                    {
                        server.Shutdown(SocketShutdown.Both);
                        server.Close();
                    }
                }
            }
            catch (Exception)
            {
                return -1; // Error general
            }

        }
        public int login(string usuario1, string contrasena)
        {
            try
            {
                ConnectToServer(); // Ensure the server is connected

                string mensaje = "1/" + usuario1 + "/" + contrasena;
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                byte[] msg2 = new byte[80];
                int bytesRecibidos = server.Receive(msg2);
                if (bytesRecibidos == 0)
                {
                    return -1; // Error en la conexión
                }

                mensaje = Encoding.ASCII.GetString(msg2).Split('\0')[0];
                if (mensaje == "0")
                {
                    conectado = true;
                    usuario = usuario1;
                    Debug.WriteLine("Inicio de sesión exitoso. Estado de conectado: " + conectado);
                    StartMessageListener();
                    return 0; // Inicio de sesión exitoso
                }
                else if (mensaje == "1")
                {
                    return 1; // Credenciales incorrectas
                }
                else if (mensaje == "2")
                {
                    return 2; // Usuario no existe
                }
                else
                {
                    return -1; // Error
                }
            }
            catch (SocketException)
            {
                return -1; // Error de conexión
            }
        }
        public int friends()
        {
            try
            {
                ConnectToServer(); // Conectar al servidor si no está conectado

                string mensaje = "5/brr/brr";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine("Peticion enviada correctamente: " + msg);
                return 0;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public int estado(string usuario, string estado)
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

        public void FriendsListPanel(bool visible, string friends)
        {
            Panel panel = panels.FirstOrDefault(p => p.Children.Any(c => c is Header header && header.Text == "Friends"));

            if (panel == null)
            {
                // Crear el panel si no existe
                panel = new Panel(new Vector2(300, -1), PanelSkin.Simple, Anchor.CenterRight);
                panel.Padding = Vector2.Zero;
                panel.Visible = visible;
                panels.Add(panel);
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
                    MainMenuGame(true);
                };
                panel.AddChild(backBtn);
            }

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

            Debug.WriteLine("FriendList Actualizada # Estado de conectado: " + conectado);

            // Asegurarse de que conectado siga siendo true
            if (!conectado)
            {
                conectado = true;
                Debug.WriteLine("Estado de conectado actualizado a TRUE en FriendsListPanel.");
            }

            // Mostrar u ocultar el panel
            panel.Visible = visible;
        }

        public void MainMenuGame(bool visible)
        {
            conectado = true;
            Debug.WriteLine("MainMenu # Estado de conectado: " + conectado);

            if (conectado)
            {
                int estado = this.estado(usuario, "1");
                ConnectToServer();
                StartMessageListener();
            }

            int friends = this.friends();

            // create top panel
            int topPanelHeight = 65;
            Panel topPanel = new Panel(new Vector2(0, topPanelHeight + 2), PanelSkin.Simple, Anchor.TopCenter);
            topPanel.Padding = Vector2.Zero;
            UserInterface.Active.AddEntity(topPanel);
            topPanel.Visible = visible;

            Button playBtn = new Button("Play", ButtonSkin.Default, Anchor.Auto, new Vector2(300, topPanelHeight));
            playBtn.OnClick = (Entity btn) => { topPanel.Visible = false; this.Game(true); };
            topPanel.AddChild(playBtn);

            Button listfriendsBtn = new Button("Friends", ButtonSkin.Default, Anchor.TopRight, new Vector2(300, topPanelHeight));
            listfriendsBtn.OnClick = (Entity btn) =>
            {
                friends = this.friends();
            };
            topPanel.AddChild(listfriendsBtn);
        }

        public void Game(bool visible)
        {
            /*enjuego = true;

            Texture2D cardsTexture = Content.Load<Texture2D>("cards");

            _ace = new MonoGame.Extended.Graphics.Texture2DRegion(cardsTexture, 384, 64, 32, 32);
            _king = new MonoGame.Extended.Graphics.Texture2DRegion(cardsTexture, 384, 32, 32, 32);
            _queen = new MonoGame.Extended.Graphics.Texture2DRegion(cardsTexture, 384, 0, 32, 32);
            _joker = new MonoGame.Extended.Graphics.Texture2DRegion(cardsTexture, 384, 96, 32, 32);
            _demon = new MonoGame.Extended.Graphics.Texture2DRegion(cardsTexture, 384, 96, 32, 32);*/


            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            panels.Add(panel);
            UserInterface.Active.AddEntity(panel);

            // text input example
            panel.AddChild(new Header("Game"));
            panel.AddChild(new HorizontalLine());

            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                MainMenuGame(true);
            };
            panel.AddChild(backBtn);
        }

        public void SignUpPanel(bool visible)
        {
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            panels.Add(panel);
            UserInterface.Active.AddEntity(panel);

            // text input example
            panel.AddChild(new Header("Sign Up"));
            panel.AddChild(new HorizontalLine());

            // inliner
            panel.AddChild(new Paragraph("User:"));
            TextInput text = new TextInput(false);
            text.PlaceholderText = "Insert user..";
            panel.AddChild(text);

            // with hidden password chars
            panel.AddChild(new Paragraph("Password:"));
            TextInput hiddenText = new TextInput(false);
            hiddenText.PlaceholderText = "Enter password..";
            hiddenText.HideInputWithChar = '*';
            panel.AddChild(hiddenText);

            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                panels[0].Visible = true;
            };
            panel.AddChild(backBtn);

            Button loginBtn = new Button("Login", ButtonSkin.Default);
            loginBtn.OnClick = (Entity btn) =>
            {
                LoginPanel(true);
                panel.Visible = false;
            };
            panel.AddChild(loginBtn);

            Button signupBtn = new Button("Sign Up", ButtonSkin.Default);
            signupBtn.OnClick = (Entity btn) =>
            {
                // Show the signup panel here
                string usuario = text.Value;
                string contrasena = hiddenText.Value;

                // Llamar al método login
                int result = signup(usuario, contrasena);

                // Manejar el resultado
                if (result == 0)
                {
                    panel.Visible = false;
                    Utils.MessageBox.ShowMsgBox("Sign Up successful", "Success");
                    this.LoginPanel(true);
                }
                else if (result == 1)
                {
                    Utils.MessageBox.ShowMsgBox("Sign Up failed", "User already exists");
                    panel.Visible = false;
                    this.SignUpPanel(true);
                }
                else if (result == 2)
                {
                    Utils.MessageBox.ShowMsgBox("Connection error", "Error");
                    panel.Visible = false;
                    this.SignUpPanel(true);
                }
                else
                {
                    Utils.MessageBox.ShowMsgBox("Connection error", "Error");
                    panel.Visible = false;
                    panels[0].Visible = true; // Volver al panel principal
                }

            };
            panel.AddChild(signupBtn);
        }
        public void LoginPanel(bool visible)
        {

            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            panels.Add(panel);
            UserInterface.Active.AddEntity(panel);

            // text input example
            panel.AddChild(new Header("Login"));
            panel.AddChild(new HorizontalLine());

            // inliner
            panel.AddChild(new Paragraph("User:"));
            TextInput text = new TextInput(false);
            text.PlaceholderText = "Insert user..";
            panel.AddChild(text);

            // with hidden password chars
            panel.AddChild(new Paragraph("Password:"));
            TextInput hiddenText = new TextInput(false);
            hiddenText.PlaceholderText = "Enter password..";
            hiddenText.HideInputWithChar = '*';
            panel.AddChild(hiddenText);
            var hideCheckbox = new CheckBox("Hide password", isChecked: true);
            hideCheckbox.OnValueChange += (Entity ent) =>
            {
                if (hideCheckbox.Checked)
                    hiddenText.HideInputWithChar = '*';
                else
                    hiddenText.HideInputWithChar = null;
            };
            panel.AddChild(hideCheckbox);

            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                panels[0].Visible = true;
            };
            panel.AddChild(backBtn);

            Button signupBtn = new Button("Sign Up", ButtonSkin.Default);
            signupBtn.OnClick = (Entity btn) =>
            {
                this.SignUpPanel(true);
                panel.Visible = false;
            };
            panel.AddChild(signupBtn);

            Button loginBtn = new Button("Login", ButtonSkin.Default);
            loginBtn.OnClick = (Entity btn) =>
            {
                string usuario1 = text.Value;
                string contrasena = hiddenText.Value;

                // Llamar al método login
                int result = login(usuario1, contrasena);

                // Manejar el resultado
                if (result == 0)
                {
                    conectado = true;
                    Debug.WriteLine("Login exitoso. Estado de conectado: " + conectado);
                    usuario = text.Value;
                    Debug.WriteLine("Login exitoso. Cambiando a MainMenuGame.");
                    Utils.MessageBox.ShowMsgBox("Login successful", "Success");
                    panel.Visible = false;
                    int estado = this.estado(usuario, "1");
                    this.MainMenuGame(true);
                }
                else if (result == 1)
                {
                    Utils.MessageBox.ShowMsgBox("Login failed", "Invalid credentials");
                }
                else if (result == 2)
                {
                    Utils.MessageBox.ShowMsgBox("User does not exist", "Error");
                }
                else
                {
                    Utils.MessageBox.ShowMsgBox("Connection error", "Error");
                }
            };
            panel.AddChild(loginBtn);
        }

        public void Options(bool visible)
        {
            // create panel and add to list of panels and manager
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            panels.Add(panel);
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
                if (conectado)
                {
                    EscMenu(usuario, true);
                }
                else
                {
                    panels[0].Visible = false;
                }

            };
            changeThemeBtn.ToolTipText = "Rotate through the built-in themes.";
            panel.AddChild(changeThemeBtn);

            Button backBtn = new Button("Back", ButtonSkin.Default);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                panels[0].Visible = true;
            };
            panel.AddChild(backBtn);
        }

        public void EscMenu(string usuario, bool visible)
        {
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
            panels.Add(panel);
            UserInterface.Active.AddEntity(panel);

            // add title and text
            panel.AddChild(new Header("Main Menu"));
            panel.AddChild(new HorizontalLine());

            // add default buttons
            Button resumeBtn = new Button("Resume", ButtonSkin.Default);
            resumeBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                panels[0].Visible = false;
                this.MainMenuGame(true);
            };
            panel.AddChild(resumeBtn);

            Button optionsBtn = new Button("Options", ButtonSkin.Default);
            optionsBtn.OnClick = (Entity btn) =>
            {
                Options(true);
                panel.Visible = false;
            };
            panel.AddChild(optionsBtn);

            Button ExitBtn = new Button("Exit", ButtonSkin.Default);
            ExitBtn.OnClick = (Entity btn) =>
            {
                int estado = this.estado(usuario, "0");
                Debug.WriteLine("Estado enviado: " + estado);
                conectado = false;
                DisconnectFromServer(); // Desconectar del servidor
                panel.Visible = false;
                Exit();
            };
            panel.AddChild(ExitBtn);
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

                Debug.WriteLine("Procesando mensaje del servidor: " + message);

                // Analizar el tipo de mensaje
                if (message.StartsWith("LIST/"))
                {
                    // Mensaje de lista de amigos
                    string friends = message.Substring(5); // Eliminar el prefijo "LIST/"
                    Debug.WriteLine("Lista de amigos recibida: " + friends);

                    // Actualizar la lista de amigos
                    FriendsListPanel(true, friends);

                    // Asegurarse de que conectado siga siendo true
                    if (!conectado)
                    {
                        conectado = true;
                        Debug.WriteLine("Estado de conectado actualizado a TRUE después de procesar el mensaje.");
                    }
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

        public void StartMessageListener()
        {
            stopMessageListener = false; // Reiniciar el indicador al iniciar el hilo

            messageListenerThread = new Thread(() =>
            {
                try
                {
                    while (conectado && !stopMessageListener)
                    {
                        try
                        {
                            // Buffer para almacenar los datos entrantes
                            byte[] buffer = new byte[1024];
                            int bytesReceived = server.Receive(buffer);

                            if (bytesReceived > 0)
                            {
                                Debug.WriteLine("Datos recibidos del servidor: " + bytesReceived + " bytes.");
                                // Convertir los datos recibidos a una cadena
                                string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);

                                // Procesar el mensaje recibido
                                ProcessServerMessage(message);
                            }
                        }
                        catch (SocketException ex)
                        {
                            Debug.WriteLine("Error en el hilo de mensajes (SocketException): " + ex.Message);

                            // Intentar reconectar
                            ReconnectToServer();

                            // Salir del bucle si no se pudo reconectar
                            if (!conectado)
                            {
                                break;
                            }
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

        /// <summary>
        /// Called after we change current example index, to hide all examples
        /// except for the currently active example + disable prev / next buttons if
        /// needed (if first or last example).
        /// </summary>
        protected void UpdateAfterExampleChange()
        {
            // hide all panels and show current example panel
            foreach (Panel panel in panels)
            {
                panel.Visible = false;
            }
            panels[currExample].Visible = true;

            // disable / enable next and previous buttons

            previousExampleButton.Enabled = currExample != 0;
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // make sure window is focused
            if (!IsActive)
                return;

            // exit on escape
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                if (conectado)
                {
                    EscMenu(usuario, true);
                    panels[0].Visible = false;
                }
                else
                {
                    Exit();
                }
            }

            // update UI
            UserInterface.Active.Update(gameTime);

            // show currently active entity (for testing)
            //targetEntityShow.Text = "Target Entity: " + (UserInterface.Active.TargetEntity != null ? UserInterface.Active.TargetEntity.GetType().Name : "null");

            // call base update
            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            // draw ui
            UserInterface.Active.Draw(spriteBatch);

            // clear buffer
            if (conectado)
                GraphicsDevice.Clear(Color.Green);
            else
                GraphicsDevice.Clear(Color.Black);

            // draw game world
            if (enjuego)
            {
                GraphicsDevice.Clear(Color.Black);
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);

                /*spriteBatch.Draw(_ace.Texture, new Vector2(336, 284), _ace.Bounds, Color.White);
                spriteBatch.Draw(_king.Texture, new Vector2(368, 284), _king.Bounds, Color.White);
                spriteBatch.Draw(_queen.Texture, new Vector2(400, 284), _queen.Bounds, Color.White);
                spriteBatch.Draw(_joker.Texture, new Vector2(432, 284), _joker.Bounds, Color.White);
                spriteBatch.Draw(_demon.Texture, new Vector2(464, 284), _demon.Bounds, Color.White);*/

                spriteBatch.End();

            }


            // finalize ui rendering
            UserInterface.Active.DrawMainRenderTarget(spriteBatch);

            // call base draw function
            base.Draw(gameTime);
        }
    }
}