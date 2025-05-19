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
    public class TitleScreen : GameScreen
    {
        private SpriteBatch _spriteBatch;
        private Texture2D _background;
        private SpriteBatch spriteBatch;

        private BuiltinThemes _currTheme;

        private Socket server;
        private Thread atender;

        public string usuario;

        public TitleScreen(Game game)
            : base(game)
        {
            game.IsMouseVisible = false;
        }

        public override void LoadContent()
        {
            base.LoadContent();

            // Inicializar UserInterface si no está inicializado
            if (UserInterface.Active == null)
            {
                //UserInterface.Initialize(Content, BuiltinThemes.hd);
                InitializeThemeAndUI(BuiltinThemes.hd);
            }

            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _background = Content.Load<Texture2D>("bg");

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
            Panel _mainMenuPanel = new Panel(new Vector2(450, -1)) { Identifier = "MainMenuPanel" };
            UserInterface.Active.AddEntity(_mainMenuPanel);
            _mainMenuPanel.Visible = visible;

            // Crear el panel principal
            Panel panel = new Panel(new Vector2(450, -1)) { Identifier = "MainMenuPanel" };
            UserInterface.Active.AddEntity(panel);

            // Agregar título y botones
            panel.AddChild(new Header("Main Menu"));
            panel.AddChild(new HorizontalLine());

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

            Button exitBtn = new Button("Exit", ButtonSkin.Default);
            exitBtn.OnClick = (Entity btn) =>
            {
                Game.Exit();
            };
            panel.AddChild(exitBtn);
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

        private void LoginPanel(bool visible)
        {
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
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
                Menu(true); // Regresar al menú principal
            };
            panel.AddChild(backBtn);

            Button signupBtn = new Button("Sign Up", ButtonSkin.Default);
            signupBtn.OnClick = (Entity btn) =>
            {
                SignUpPanel(true);
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
                    usuario = text.Value;
                    GeonBit.UI.Utils.MessageBox.ShowMsgBox("Login successful", "Success");

                    // Eliminar todos los elementos de la interfaz de usuario
                    UserInterface.Active.Clear();

                    // Cambiar a la pantalla principal
                    ScreenManager.LoadScreen(new MainMenuScreen(Game, usuario), new FadeTransition(GraphicsDevice, Color.Black, 0.5f));
                }
                else if (result == 1)
                {
                    GeonBit.UI.Utils.MessageBox.ShowMsgBox("Login failed", "Invalid credentials");
                }
                else if (result == 2)
                {
                    GeonBit.UI.Utils.MessageBox.ShowMsgBox("User does not exist", "Error");
                }
                else
                {
                    GeonBit.UI.Utils.MessageBox.ShowMsgBox("Connection error", "Error");
                }
            };
            panel.AddChild(loginBtn);
        }

        private void SignUpPanel(bool visible)
        {
            // create panel and add to list of panels and manager
            Panel panel = new Panel(new Vector2(450, -1));
            panel.Visible = visible;
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
                Menu(true); // Regresar al menú principal
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
                    GeonBit.UI.Utils.MessageBox.ShowMsgBox("Sign Up successful", "Success");
                    LoginPanel(true);
                }
                else if (result == 1)
                {
                    GeonBit.UI.Utils.MessageBox.ShowMsgBox("Sign Up failed", "User already exists");
                    panel.Visible = false;
                    SignUpPanel(true);
                }
                else if (result == 2)
                {
                    GeonBit.UI.Utils.MessageBox.ShowMsgBox("Connection error", "Error");
                    panel.Visible = false;
                    SignUpPanel(true);
                }
                else
                {
                    GeonBit.UI.Utils.MessageBox.ShowMsgBox("Connection error", "Error");
                    panel.Visible = false;
                    Menu(true); // Volver al panel principal
                }

            };
            panel.AddChild(signupBtn);
        }

        private int signup(string usuario1, string contrasena)
        {
            try
            {
                IPAddress direc = IPAddress.Parse("84.235.233.248"); //10.4.119.5
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
        private int login(string usuario1, string contrasena)
        {
            try
            {
                IPAddress direc = IPAddress.Parse("84.235.233.248"); //10.4.119.5
                IPEndPoint ipep = new IPEndPoint(direc, 50756);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    server.Connect(ipep);

                    if (!server.Connected)
                    {
                        return -1; // Error de conexión
                    }

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
                        usuario = usuario1;

                        // NO CERRAR EL SOCKET AQUÍ - Pasar el socket a MainMenuScreen
                        Socket socketParaMainMenu = server;
                        server = null; // Evitar que se cierre en finally

                        // Cambiar a la pantalla principal
                        MainMenuScreen mainMenuScreen = new MainMenuScreen(Game, usuario);
                        mainMenuScreen.SetExistingSocket(socketParaMainMenu); // Método nuevo a implementar

                        GeonBit.UI.Utils.MessageBox.ShowMsgBox("Login successful", "Success");
                        UserInterface.Active.Clear();
                        ScreenManager.LoadScreen(mainMenuScreen, new FadeTransition(GraphicsDevice, Color.Black, 0.5f));

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
                finally
                {
                    // Solo cerrar el socket si no fue transferido a MainMenuScreen
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

        public override void Update(GameTime gameTime)
        {
            var mouseState = MouseExtended.GetState();
            var keyboardState = KeyboardExtended.GetState();

            //if (keyboardState.WasKeyReleased(Keys.Escape))
            //Game.Exit();

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
    }
}
