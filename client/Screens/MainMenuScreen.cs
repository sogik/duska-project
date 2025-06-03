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
        private string amigoActualConsulta = "";

        private Socket server;
        private Thread atender;
        private bool isReconnecting = false;
        private volatile bool stopMessageListener = false;
        private Thread messageListenerThread;

        public string usuario;
        public string destinatario;
        private bool esLider = false;

        private bool conectado = false; // Indica si el cliente está conectado al servidor

        // 1. Añadir una nueva variable para controlar desconexiones intencionales
        private volatile bool desconexionIntencional = false;

        private List<string> jugadoresGrupo = new List<string>();
        private Panel infoGrupoPanel = null;
        private Paragraph labelJugadores = null;

        public MainMenuScreen(Game game, string usuario)
            : base(game)
        {
            this.usuario = usuario;
            game.IsMouseVisible = false;

            // **DEBUG TEMPORAL**
            Debug.WriteLine($"[CONSTRUCTOR] MainMenuScreen creado para usuario: {usuario}");
            Debug.WriteLine($"[CONSTRUCTOR] Socket inicial: {(server != null ? "Existe" : "Null")}");
        }

        public override void LoadContent()
        {
            base.LoadContent();

            // IMPORTANTE: Limpiar y reinicializar la interfaz de usuario SIEMPRE
            if (UserInterface.Active != null)
            {
                UserInterface.Active.Clear();
                Debug.WriteLine("[MAIN MENU] UI existente limpiada");
            }

            // Reinicializar la interfaz de usuario
            InitializeThemeAndUI(BuiltinThemes.hd);

            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _background = Content.Load<Texture2D>("bg2");

            // **SOLO CONECTAR SI NO HAY SOCKET VÁLIDO**
            if (server == null || !conectado || !server.Connected)
            {
                Debug.WriteLine("[MAIN MENU] No hay socket válido, creando nueva conexión");
                int estado1 = this.estado(usuario, "1");
                ConnectToServer();
                StartMessageListener();
            }
            else
            {
                Debug.WriteLine("[MAIN MENU] Socket existente válido, reutilizando conexión");
                // **ASEGURAR QUE EL HILO DE ESCUCHA ESTÉ ACTIVO**
                if (!messageListenerRunning)
                {
                    StartMessageListener();
                }
            }

            SolicitarEstadoLider();
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
            playBtn.Identifier = "playBtn";
            playBtn.OnClick = (Entity btn) =>
            {
                if (esLider)
                {
                    try
                    {
                        // Formato: 20/usuario/
                        // Este es el código para iniciar una partida según partidas.c
                        string iniciarPartidaMsg = "20/" + usuario + "/";
                        byte[] msg = Encoding.ASCII.GetBytes(iniciarPartidaMsg);
                        server.Send(msg);
                        Debug.WriteLine("[JUEGO] Solicitud para iniciar partida enviada al servidor");

                        // El mensaje de confirmación START_GAME_OK iniciará la transición
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ERROR] Error al iniciar partida: {ex.Message}");
                        GeonBit.UI.Utils.MessageBox.ShowMsgBox(
                            "No se pudo iniciar la partida. Inténtalo de nuevo.",
                            "Error de conexión"
                        );
                    }
                }
                else
                {
                    GeonBit.UI.Utils.MessageBox.ShowMsgBox(
                        "Solo el líder puede iniciar la partida.",
                        "No eres el líder"
                    );
                }
            };
            topPanel.AddChild(playBtn);

            // Actualizar estado inicial del botón
            ActualizarEstadoLider();

            Button listfriendsBtn = new Button("Friends", ButtonSkin.Default, Anchor.TopRight, new Vector2(300, topPanelHeight));
            listfriendsBtn.OnClick = (Entity btn) =>
            {
                friends = this.friends();
            };
            topPanel.AddChild(listfriendsBtn);

            CrearPanelInfoGrupo(visible);
        }

        // Agregar este nuevo método:

        private void CrearPanelInfoGrupo(bool visible)
        {
            try
            {
                // **ELIMINAR PANEL ANTERIOR SI EXISTE**
                if (infoGrupoPanel != null)
                {
                    UserInterface.Active.RemoveEntity(infoGrupoPanel);
                    infoGrupoPanel = null;
                    labelJugadores = null;
                }

                // **CREAR PANEL PARA INFORMACIÓN DEL GRUPO**
                infoGrupoPanel = new Panel(new Vector2(400, 120), PanelSkin.Simple, Anchor.TopCenter);
                infoGrupoPanel.Offset = new Vector2(0, 80); // Debajo del panel principal
                infoGrupoPanel.Padding = new Vector2(10, 10);
                infoGrupoPanel.Visible = visible;
                infoGrupoPanel.Identifier = "InfoGrupoPanel";
                UserInterface.Active.AddEntity(infoGrupoPanel);

                // **TÍTULO DEL PANEL**
                Header titulo = new Header("Tu Grupo de Juego");
                titulo.Scale = 0.9f;
                titulo.FillColor = Color.LightBlue;
                infoGrupoPanel.AddChild(titulo);

                infoGrupoPanel.AddChild(new HorizontalLine());

                // **LABEL PARA MOSTRAR JUGADORES**
                labelJugadores = new Paragraph("Cargando información del grupo...");
                labelJugadores.Scale = 0.8f;
                labelJugadores.FillColor = Color.White;
                labelJugadores.WrapWords = true;
                infoGrupoPanel.AddChild(labelJugadores);

                // **LABEL PARA MOSTRAR ESTADO DE LÍDER**
                Paragraph labelLider = new Paragraph(esLider ? "Eres el LÍDER del grupo" : "Eres MIEMBRO del grupo");
                labelLider.Scale = 0.75f;
                labelLider.FillColor = esLider ? Color.Green : Color.Yellow;
                labelLider.Identifier = "LabelLider";
                infoGrupoPanel.AddChild(labelLider);

                Debug.WriteLine("[GRUPO UI] Panel de información del grupo creado");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error creando panel de grupo: {ex.Message}");
            }
        }

        private void SolicitarEstadoLider()
        {
            try
            {
                if (conectado)
                {
                    string mensaje = "22/" + usuario + "/CHECK_LEADER";
                    byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);
                    Debug.WriteLine("Solicitud de estado de líder enviada");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al solicitar estado de líder: " + ex.Message);
            }
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

            Button bajaBtn = new Button("Dar de Baja Cuenta", ButtonSkin.Default);
            bajaBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                MostrarConfirmacionBaja();
            };
            panel.AddChild(bajaBtn);

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
                // Marcar como desconexión intencional y desconectar
                DisconnectFromServer();

                // Limpiar UI
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);

                // Esperar un poco para asegurar que la desconexión se complete
                Thread.Sleep(200);

                // Salir del juego
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

        private void MostrarConfirmacionBaja()
        {
            // Crear panel de confirmación simple
            Panel panel = new Panel(new Vector2(400, 200), PanelSkin.Default, Anchor.Center);
            panel.Visible = true;
            UserInterface.Active.AddEntity(panel);

            panel.AddChild(new Header("Eliminar Cuenta"));
            panel.AddChild(new HorizontalLine());
            panel.AddChild(new Paragraph("Estas seguro que quieres eliminar tu cuenta?"));
            panel.AddChild(new Paragraph("Esta accion no se puede deshacer."));
            panel.AddChild(new HorizontalLine());

            // Panel para los botones
            Panel botonesPanel = new Panel(new Vector2(0, 60), PanelSkin.None);

            // Botón SÍ
            Button siBtn = new Button("SI", ButtonSkin.Default);
            siBtn.Size = new Vector2(120, 40);
            siBtn.FillColor = Color.Red;
            siBtn.Anchor = Anchor.TopLeft;
            siBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
                EjecutarEliminacionCuenta();
            };
            botonesPanel.AddChild(siBtn);

            // Botón NO
            Button noBtn = new Button("NO", ButtonSkin.Default);
            noBtn.Size = new Vector2(120, 40);
            noBtn.Anchor = Anchor.TopRight;
            noBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
            };
            botonesPanel.AddChild(noBtn);

            panel.AddChild(botonesPanel);

            Debug.WriteLine("[BAJA] Panel de confirmación mostrado");
        }

        // **MÉTODO SIMPLE PARA MOSTRAR ERRORES**
        private void MostrarMensajeError(string mensaje)
        {
            Panel panelError = new Panel(new Vector2(400, 150), PanelSkin.Default, Anchor.Center);
            panelError.Visible = true;
            UserInterface.Active.AddEntity(panelError);

            Header titulo = new Header("Error");
            titulo.FillColor = Color.Red;
            panelError.AddChild(titulo);
            panelError.AddChild(new HorizontalLine());

            panelError.AddChild(new Paragraph(mensaje));

            Button okBtn = new Button("OK");
            okBtn.OnClick = (Entity btn) =>
            {
                panelError.Visible = false;
                UserInterface.Active.RemoveEntity(panelError);
            };
            panelError.AddChild(okBtn);

            // Auto-cerrar después de 3 segundos
            System.Threading.Timer timer = new System.Threading.Timer((state) =>
            {
                try
                {
                    if (panelError.Visible)
                    {
                        panelError.Visible = false;
                        UserInterface.Active.RemoveEntity(panelError);
                    }
                }
                catch { }
            }, null, 3000, System.Threading.Timeout.Infinite);
        }

        private void EjecutarEliminacionCuenta()
        {
            try
            {
                Debug.WriteLine("[BAJA] Eliminando cuenta");

                if (server != null && server.Connected)
                {
                    // Mensaje súper simple
                    string mensaje = $"29/{usuario}/brr";
                    byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);

                    Debug.WriteLine($"[BAJA] Enviado: {mensaje}");
                }
                else
                {
                    MostrarMensajeError("Error de conexion");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] {ex.Message}");
                MostrarMensajeError("Error al eliminar cuenta");
            }
        }

        private void MostrarPanelProcesandoBaja()
        {
            // Limpiar cualquier panel anterior
            var panelAnterior = UserInterface.Active.Root.Children
                .OfType<Panel>()
                .FirstOrDefault(p => p.Identifier == "PanelProcesandoBaja");

            if (panelAnterior != null)
            {
                UserInterface.Active.RemoveEntity(panelAnterior);
            }

            // Crear panel de carga
            Panel panel = new Panel(new Vector2(400, 200), PanelSkin.Default, Anchor.Center);
            panel.Visible = true;
            panel.Identifier = "PanelProcesandoBaja";
            UserInterface.Active.AddEntity(panel);

            panel.AddChild(new Header("Procesando..."));
            panel.AddChild(new HorizontalLine());
            panel.AddChild(new Paragraph("Eliminando tu cuenta del servidor..."));
            panel.AddChild(new Paragraph("Por favor espera."));

            Debug.WriteLine("[BAJA] Panel de procesamiento mostrado");
        }

        private void ProcesarRespuestaBaja(string respuesta)
        {
            try
            {
                Debug.WriteLine($"[BAJA] Respuesta recibida: {respuesta}");

                // Cerrar panel de procesamiento si existe
                foreach (var entity in UserInterface.Active.Root.Children.ToList())
                {
                    if (entity is Panel p && p.Identifier == "PanelProcesandoBaja")
                    {
                        UserInterface.Active.RemoveEntity(p);
                        break;
                    }
                }

                if (respuesta.StartsWith("BAJA_OK/"))
                {
                    // Cuenta eliminada
                    Panel panelExito = new Panel(new Vector2(300, 150), PanelSkin.Default, Anchor.Center);
                    panelExito.Visible = true;
                    UserInterface.Active.AddEntity(panelExito);

                    panelExito.AddChild(new Header("Cuenta Eliminada"));
                    panelExito.AddChild(new Paragraph("Tu cuenta ha sido eliminada."));

                    Button cerrarBtn = new Button("Cerrar");
                    cerrarBtn.OnClick = (Entity btn) => Game.Exit();
                    panelExito.AddChild(cerrarBtn);

                    return;
                }
                else if (respuesta.StartsWith("ERROR/"))
                {
                    // Error en la eliminación
                    string mensajeError = respuesta.Substring(6);
                    Debug.WriteLine($"[BAJA] Error: {mensajeError}");

                    MostrarMensajeError($"Error al eliminar la cuenta: {mensajeError}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error procesando respuesta de baja: {ex.Message}");
                MostrarMensajeError("Error al procesar la respuesta del servidor.");
            }
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
                    // **EXCLUIR EL PROPIO USUARIO TAMBIÉN EN LA LISTA DE USUARIOS**
                    if (!string.IsNullOrWhiteSpace(friend) &&
                        !friend.Equals(usuario, StringComparison.OrdinalIgnoreCase) &&
                        friend != "LISTU" &&
                        !friend.StartsWith("Error"))
                    {
                        Debug.WriteLine($"Agregando usuario a la lista: {friend}");
                        usuariosList.AddItem(friend);
                    }
                }

                if (usuariosList.Count == 0)
                {
                    usuariosList.AddItem("No hay otros usuarios registrados");
                }
            }

            panel.Visible = visible;
        }

        // Reemplazar ListaPartidasPanel:

        private void ListaPartidasPanel(bool visible, string partidas)
        {
            // **PANEL MÁS GRANDE Y CENTRADO**
            Panel panel = new Panel(new Vector2(700, 500), PanelSkin.Default, Anchor.Center);
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // **TÍTULO MÁS VISIBLE**
            Header titulo = new Header("Lista de Partidas");
            titulo.Scale = 1.2f;
            panel.AddChild(titulo);
            panel.AddChild(new HorizontalLine());

            Paragraph descripcion = new Paragraph("Partidas registradas (con jugadores):");
            descripcion.Scale = 1.0f;
            panel.AddChild(descripcion);

            // **LISTA MÁS GRANDE**
            SelectList list = new SelectList(new Vector2(0, 350)) { Identifier = "PartidasList" };
            panel.AddChild(list);

            // **BOTÓN MÁS GRANDE**
            Button backBtn = new Button("Regresar", ButtonSkin.Default);
            backBtn.Size = new Vector2(150, 50);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
                Extras(true);
            };
            panel.AddChild(backBtn);

            SelectList partidasList = panel.Find<SelectList>("PartidasList");
            if (partidasList != null)
            {
                partidasList.ClearItems();

                // **DIVIDIR POR '/' PARA PROCESAR CADA PARTIDA**
                string[] partidasArray = partidas.Split('/');

                foreach (string partida in partidasArray)
                {
                    if (!string.IsNullOrWhiteSpace(partida) &&
                        !partida.StartsWith("ERROR") &&
                        !partida.StartsWith("LISTP") &&
                        partida.Contains("Partida"))
                    {
                        Debug.WriteLine($"Agregando partida con jugadores: {partida}");
                        partidasList.AddItem(partida);
                    }
                }

                // Si no hay partidas, mostrar mensaje
                if (partidasList.Count == 0)
                {
                    partidasList.AddItem("No hay partidas registradas");
                }
            }
        }

        private void ListaPartidasGanadasPanel(bool visible, string partidasGanadas)
        {
            // **PANEL MÁS GRANDE Y CENTRADO**
            Panel panel = new Panel(new Vector2(650, 450), PanelSkin.Default, Anchor.Center);
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // **TÍTULO MÁS VISIBLE**
            Header titulo = new Header("Mis Partidas Ganadas");
            titulo.Scale = 1.2f;
            titulo.FillColor = Color.Gold;
            panel.AddChild(titulo);
            panel.AddChild(new HorizontalLine());

            Paragraph descripcion = new Paragraph("Historial de tus victorias:");
            descripcion.Scale = 1.0f;
            descripcion.FillColor = Color.LightGreen;
            panel.AddChild(descripcion);

            // **LISTA MÁS GRANDE**
            SelectList list = new SelectList(new Vector2(0, 320)) { Identifier = "partidasGanadasList" };
            panel.AddChild(list);

            // **BOTÓN MÁS GRANDE**
            Button backBtn = new Button("Regresar", ButtonSkin.Default);
            backBtn.Size = new Vector2(150, 50);
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
                Extras(true);
            };
            panel.AddChild(backBtn);

            SelectList partidasGanadasList = panel.Find<SelectList>("partidasGanadasList");
            if (partidasGanadasList != null)
            {
                partidasGanadasList.ClearItems();

                // **DIVIDIR POR '/' COMO LAS DEMÁS LISTAS**
                string[] partidasArray = partidasGanadas.Split('/');

                foreach (string partida in partidasArray)
                {
                    if (!string.IsNullOrWhiteSpace(partida) &&
                        !partida.StartsWith("ERROR") &&
                        !partida.StartsWith("PARTIDASG"))
                    {
                        Debug.WriteLine($"Agregando partida ganada a la lista: {partida}");
                        partidasGanadasList.AddItem(partida);
                    }
                }

                // Si no hay partidas, mostrar mensaje
                if (partidasGanadasList.Count == 0)
                {
                    partidasGanadasList.AddItem("No has ganado ninguna partida aún");
                }
            }
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

                string mensaje = $"3/{usuario}/brr";
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

                    string respuesta = "ACEPTADA";

                    string remitente = mensaje2[0].Trim();

                    Invitacion(2, remitente, respuesta); // Enviar invitación al servidor

                    panel.Visible = false;
                };
                panel.AddChild(acceptBtn);

                Button declineBtn = new Button("Decline", ButtonSkin.Default);
                declineBtn.OnClick = (Entity btn) =>
                {
                    Debug.WriteLine("Invitación rechazada.");

                    string respuesta = "RECHAZADA";

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
                    // **DOBLE VERIFICACIÓN: EXCLUIR EL PROPIO USUARIO**
                    if (!string.IsNullOrWhiteSpace(friend) &&
                        !friend.Equals(usuario, StringComparison.OrdinalIgnoreCase) &&
                        friend != "LIST" &&
                        !friend.StartsWith("Error") &&
                        !friend.Contains("No hay"))
                    {
                        Debug.WriteLine($"Agregando amigo a la lista: {friend}");
                        friendsList.AddItem(friend);
                    }
                }

                // Si no hay amigos para mostrar
                if (friendsList.Count == 0)
                {
                    friendsList.AddItem("No hay otros usuarios conectados");
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

            // Crear un panel con las opciones más grande para el nuevo botón
            Panel optionsPanel = new Panel(new Vector2(200, 200), PanelSkin.Default, Anchor.Center);
            optionsPanel.Identifier = "FriendOptionsMenu";
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

            // **NUEVO BOTÓN: VER PARTIDAS JUGADAS CON ESTE AMIGO**
            Button partidasBtn = new Button("Ver Partidas Juntos", ButtonSkin.Default);
            partidasBtn.FillColor = Color.LightBlue;
            partidasBtn.OnClick = (Entity btn) =>
            {
                Debug.WriteLine($"Solicitando partidas jugadas con {friendName}...");

                try
                {
                    UserInterface.Active.RemoveEntity(optionsPanel);
                    SolicitarPartidasConAmigo(friendName);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error al solicitar partidas con amigo: {ex.Message}");
                    Menu(true); // Regresar al menú principal en caso de error
                }
            };
            optionsPanel.AddChild(partidasBtn);

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

        private void MostrarPanelCargandoPartidas(string nombreAmigo)
        {
            // **PANEL DE CARGA MÁS GRANDE**
            Panel panel = new Panel(new Vector2(500, 250), PanelSkin.Default, Anchor.Center);
            panel.Visible = true;
            panel.Identifier = "PanelCargandoPartidas";
            UserInterface.Active.AddEntity(panel);

            Header titulo = new Header("Buscando Partidas");
            titulo.Scale = 1.3f;
            titulo.FillColor = Color.Yellow;
            panel.AddChild(titulo);
            panel.AddChild(new HorizontalLine());

            Paragraph mensaje1 = new Paragraph($"Buscando partidas jugadas con {nombreAmigo}...");
            mensaje1.Scale = 1.1f;
            panel.AddChild(mensaje1);

            Paragraph mensaje2 = new Paragraph("Por favor espera un momento.");
            mensaje2.Scale = 1.0f;
            mensaje2.FillColor = Color.LightGray;
            panel.AddChild(mensaje2);

            Debug.WriteLine($"Panel de carga mostrado para buscar partidas con {nombreAmigo}");
        }

        // Reemplazar MostrarPartidasConAmigoPanel:

        private void MostrarPartidasConAmigoPanel(bool visible, string partidasAmigo, string nombreAmigo)
        {
            // Cerrar panel de carga si existe
            Entity panelCarga = UserInterface.Active.Root.Find("PanelCargandoPartidas");
            if (panelCarga != null)
            {
                UserInterface.Active.RemoveEntity(panelCarga);
            }

            // **PANEL MÁS GRANDE Y CENTRADO**
            Panel panel = new Panel(new Vector2(750, 500), PanelSkin.Default, Anchor.Center);
            panel.Visible = visible;
            UserInterface.Active.AddEntity(panel);

            // **TÍTULO MÁS VISIBLE CON EL NOMBRE DEL AMIGO**
            Header titulo = new Header($"Partidas con {nombreAmigo}");
            titulo.Scale = 1.3f;
            titulo.FillColor = Color.LightBlue;
            panel.AddChild(titulo);
            panel.AddChild(new HorizontalLine());

            Paragraph descripcion = new Paragraph($"Historial completo de partidas jugadas con {nombreAmigo}:");
            descripcion.Scale = 1.0f;
            descripcion.FillColor = Color.White;
            panel.AddChild(descripcion);

            // **LISTA MÁS GRANDE PARA VER MEJOR LOS RESULTADOS**
            SelectList list = new SelectList(new Vector2(0, 370)) { Identifier = "PartidasAmigoList" };
            panel.AddChild(list);

            // **PANEL PARA BOTONES**
            Panel botonesPanel = new Panel(new Vector2(0, 60), PanelSkin.None);

            // **BOTÓN REGRESAR MÁS GRANDE**
            Button backBtn = new Button("Regresar a Amigos", ButtonSkin.Default);
            backBtn.Size = new Vector2(200, 50);
            backBtn.Anchor = Anchor.Center;
            backBtn.OnClick = (Entity btn) =>
            {
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
                FriendsListPanel(true, "");
                friends();
            };
            botonesPanel.AddChild(backBtn);

            panel.AddChild(botonesPanel);

            // Procesar y mostrar las partidas
            SelectList partidasList = panel.Find<SelectList>("PartidasAmigoList");
            if (partidasList != null)
            {
                partidasList.ClearItems();

                // Dividir por '/' para procesar cada partida
                string[] partidasArray = partidasAmigo.Split('/');

                foreach (string partida in partidasArray)
                {
                    if (!string.IsNullOrWhiteSpace(partida) &&
                        !partida.StartsWith("ERROR") &&
                        !partida.StartsWith("PARTIDASAMIGO") &&
                        partida.Contains("Partida"))
                    {
                        Debug.WriteLine($"Agregando partida con amigo: {partida}");

                        // **FORMATEAR MEJOR SEGÚN EL RESULTADO SIN EMOJIS**
                        string texto = partida;
                        if (partida.Contains("GANASTE"))
                        {
                            texto = "[VICTORIA] " + partida.Replace("GANASTE", "GANASTE");
                        }
                        else if (partida.Contains("PERDISTE"))
                        {
                            texto = "[DERROTA] " + partida.Replace("PERDISTE", "PERDISTE");
                        }
                        else if (partida.Contains("EMPATE"))
                        {
                            texto = "[EMPATE] " + partida.Replace("EMPATE", "EMPATE");
                        }

                        partidasList.AddItem(texto);
                    }
                }

                // Si no hay partidas
                if (partidasList.Count == 0)
                {
                    partidasList.AddItem($"No hay partidas jugadas con {nombreAmigo}");
                }

                Debug.WriteLine($"Panel de partidas con {nombreAmigo} mostrado con {partidasList.Count} entradas");
            }
        }

        private int SolicitarPartidasConAmigo(string nombreAmigo)
        {
            try
            {
                // **GUARDAR EL NOMBRE DEL AMIGO PARA LA RESPUESTA**
                amigoActualConsulta = nombreAmigo;

                if (!conectado)
                {
                    ConnectToServer();
                }

                MostrarPanelCargandoPartidas(nombreAmigo);

                string mensaje = $"30/{usuario}/{nombreAmigo}";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine($"Petición de partidas con amigo enviada: {mensaje}");
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al enviar petición de partidas con amigo: {ex.Message}");
                conectado = false;
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
            // No intentar conectar si fue una desconexión intencional
            if (desconexionIntencional)
            {
                Debug.WriteLine("No se intenta conectar: la desconexión fue intencional");
                return;
            }

            // Solo intentar conectar si no hay conexión activa
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

                    string mensaje = "0/" + usuario;
                    byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);

                    // Una vez conectado, solicitar información de sala/líder
                    SolicitarInformacionSala();
                }
                catch (SocketException ex)
                {
                    Debug.WriteLine("Error al conectar al servidor: " + ex.Message);
                    conectado = false;
                    throw new Exception("No se pudo conectar al servidor.");
                }
            }
        }

        private void SolicitarInformacionSala()
        {
            try
            {
                if (conectado && server != null && server.Connected)
                {
                    // Código 10: Solicitud de información de sala
                    string mensaje = "10/" + usuario;
                    byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);
                    Debug.WriteLine("[RED] Solicitud de información de sala enviada");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al solicitar información de sala: {ex.Message}");
            }
        }

        private void DisconnectFromServer()
        {
            try
            {
                Debug.WriteLine("[DESCONEXION] Iniciando desconexión limpia");

                stopMessageListener = true;
                conectado = false;

                if (server != null)
                {
                    if (server.Connected)
                    {
                        // Enviar mensaje de desconexión si es posible
                        try
                        {
                            string mensaje = "DISCONNECT/" + usuario;
                            byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                            server.Send(msg);

                            // Dar tiempo para que llegue el mensaje
                            System.Threading.Thread.Sleep(100);
                        }
                        catch
                        {
                            // Ignorar errores al enviar mensaje de desconexión
                        }

                        server.Shutdown(SocketShutdown.Both);
                    }

                    server.Close();
                    server = null;
                }

                Debug.WriteLine("[DESCONEXION] Desconexión completada");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en desconexión: {ex.Message}");
            }
        }

        private void ReconnectToServer()
        {
            // No reconectar si fue una desconexión intencional o ya está reconectando
            if (isReconnecting || desconexionIntencional)
            {
                Debug.WriteLine("No se intenta reconectar: desconexión intencional o ya en proceso.");
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

        private void SolicitarInformacionLider(string grupoId)
        {
            try
            {
                if (conectado && server != null && server.Connected)
                {
                    // Enviar solicitud para obtener información del líder
                    // Formato: 12/usuario/
                    string mensaje = "12/" + usuario + "/";
                    byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);
                    Debug.WriteLine("[GRUPO] Solicitud de información del líder enviada");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al solicitar información de líder: {ex.Message}");
            }
        }

        private void ProcessServerMessage(string message)
        {
            Debug.WriteLine($"[RED] Mensaje recibido: {message}");

            // Mensajes del tipo CARDS/ son manejados por GameCardScreen, ignorarlos aquí
            if (message.StartsWith("CARDS/")) return;

            if (message.StartsWith("TURN/")) return;

            // Manejar diferentes tipos de mensajes del servidor
            if (message.StartsWith("GRUPO_CREADO/"))
            {
                // Formato: "GRUPO_CREADO/id"
                string grupoId = message.Substring(13);
                Debug.WriteLine($"[GRUPO] Grupo creado con ID: {grupoId}");

                // Solicitar información sobre el líder del grupo
                SolicitarInformacionLider(grupoId);
                return;
            }
            else if (message.StartsWith("BAJA_OK/") ||
                (message.StartsWith("ERROR/") && (message.Contains("eliminacion") || message.Contains("Confirmacion"))))
            {
                // Respuesta sobre eliminación de cuenta
                ProcesarRespuestaBaja(message);
                return;
            }
            else if (message.StartsWith("PARTIDASAMIGO/"))
            {
                string partidasAmigo = message.Substring(14);
                Debug.WriteLine("[PARTIDAS AMIGO] Lista de partidas con amigo recibida");

                // Usar el nombre guardado
                string nombreAmigo = !string.IsNullOrEmpty(amigoActualConsulta) ? amigoActualConsulta : "amigo";

                MostrarPartidasConAmigoPanel(true, partidasAmigo, nombreAmigo);

                // Limpiar la variable
                amigoActualConsulta = "";
                return;
            }
            else if (message.StartsWith("GRUPO/"))
            {
                // Formato: "GRUPO/id/usuario1/usuario2/..."
                string[] partes = message.Split('/');
                if (partes.Length >= 3)
                {
                    string grupoId = partes[1];

                    // **ACTUALIZAR LISTA DE JUGADORES DEL GRUPO**
                    jugadoresGrupo.Clear();
                    for (int i = 2; i < partes.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(partes[i]))
                        {
                            jugadoresGrupo.Add(partes[i].Trim());
                        }
                    }

                    // El primer usuario listado es el líder según el servidor
                    string primerUsuario = jugadoresGrupo.Count > 0 ? jugadoresGrupo[0] : "";
                    esLider = primerUsuario.Equals(usuario, StringComparison.OrdinalIgnoreCase);

                    Debug.WriteLine($"[GRUPO] Grupo {grupoId}, jugadores: {string.Join(", ", jugadoresGrupo)}");
                    Debug.WriteLine($"[GRUPO] Líder: {primerUsuario}, ¿Soy líder? {esLider}");

                    // **ACTUALIZAR LA INTERFAZ**
                    ActualizarEstadoLider();
                    return;
                }
            }
            else if (message.StartsWith("LEADER/"))
            {
                // Formato: "LEADER/nombreUsuario"
                string nombreLider = message.Substring(7);
                esLider = nombreLider.Trim().Equals(usuario.Trim(), StringComparison.OrdinalIgnoreCase);

                Debug.WriteLine($"[GRUPO] Información de líder: {nombreLider}, ¿Soy líder? {esLider}");

                // **REORGANIZAR LISTA PARA QUE EL LÍDER ESTÉ PRIMERO**
                if (jugadoresGrupo.Count > 0)
                {
                    jugadoresGrupo.RemoveAll(j => j.Equals(nombreLider, StringComparison.OrdinalIgnoreCase));
                    jugadoresGrupo.Insert(0, nombreLider);
                }

                // Actualizar la interfaz según el estado de líder
                ActualizarEstadoLider();
                return;
            }
            else if (message.StartsWith("GRUPO_SALIDA/"))
            {
                // Formato: "GRUPO_SALIDA/nombreUsuario"
                string nombreUsuario = message.Substring(13);
                Debug.WriteLine($"[GRUPO] El usuario {nombreUsuario} ha salido del grupo");

                // **REMOVER USUARIO DE LA LISTA LOCAL**
                jugadoresGrupo.RemoveAll(j => j.Equals(nombreUsuario, StringComparison.OrdinalIgnoreCase));

                // **ACTUALIZAR INTERFAZ**
                ActualizarPanelInfoGrupo();

                // Si alguien sale del grupo, verificar si ahora somos líder
                SolicitarInformacionSala();
                return;
            }
            else if (message.StartsWith("START_GAME_OK") || message.StartsWith("GAME_STARTED") ||
        message.Contains("PARTIDA_INICIADA") || message.StartsWith("GAMESTART/"))
            {
                Debug.WriteLine("[JUEGO] Notificación de inicio de partida recibida");

                // **LIMPIAR INFORMACIÓN DEL GRUPO AL INICIAR PARTIDA**
                jugadoresGrupo.Clear();

                // Cambiar a la pantalla de juego
                try
                {
                    // ... resto del código existente ...
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Error al cambiar a pantalla de juego: {ex.Message}");
                }
                return;
            }
            else if (message.StartsWith("LIST/"))
            {
                // Mensaje de lista de amigos
                string friends = message.Substring(5);
                Debug.WriteLine("[AMIGOS] Lista de amigos recibida");

                // Actualizar la lista de amigos
                FriendsListPanel(true, friends);
                return;
            }
            else if (message.StartsWith("LISTU/"))
            {
                // Mensaje de lista de usuarios
                string usuarios = message.Substring(6);
                Debug.WriteLine("[USUARIOS] Lista de usuarios recibida");

                // Actualizar la lista de usuarios
                ListaUsuariosPanel(true, usuarios);
                return;
            }
            else if (message.StartsWith("LISTP/"))
            {
                // Mensaje de lista de partidas
                string partidas = message.Substring(6);
                Debug.WriteLine("[PARTIDAS] Lista de partidas recibida");

                // Actualizar la lista de partidas
                ListaPartidasPanel(true, partidas);
                return;
            }
            else if (message.StartsWith("LISTPG/"))
            {
                // Mensaje de lista de partidas ganadas
                string partidasGanadas = message.Substring(7);
                Debug.WriteLine("[PARTIDAS] Lista de partidas ganadas recibida");

                // Actualizar la lista de partidas ganadas
                ListaPartidasGanadasPanel(true, partidasGanadas);
                return;
            }
            else if (message.StartsWith("INV/"))
            {
                // Invitación recibida
                string invitacion = message.Substring(4);
                Debug.WriteLine($"[INVITACIÓN] *** INVITACIÓN RECIBIDA: '{invitacion}' ***");

                // **LIMPIAR UI ANTES DE MOSTRAR INVITACIÓN**
                if (UserInterface.Active != null)
                {
                    // Buscar y cerrar paneles existentes que puedan interferir
                    var panelsToRemove = UserInterface.Active.Root.Children
                        .Where(child => child is Panel panel &&
                               (panel.Identifier?.Contains("Menu") == true ||
                                panel.Identifier?.Contains("Options") == true))
                        .ToList();

                    foreach (var panel in panelsToRemove)
                    {
                        UserInterface.Active.RemoveEntity(panel);
                    }
                }

                InvitacionPanel(1, true, invitacion);
                return;
            }
            else if (message.StartsWith("INVR/"))
            {
                // Respuesta a invitación
                string respuestaInv = message.Substring(5);
                Debug.WriteLine($"[INVITACIÓN] *** RESPUESTA RECIBIDA: '{respuestaInv}' ***");

                InvitacionPanel(2, true, respuestaInv);
                return;
            }
            else if (message.StartsWith("Partida ") || message.Contains("Partida"))
            {
                Debug.WriteLine("[PARTIDAS] Lista de partidas recibida");
                ListaPartidasPanel(true, message);
                return;
            }
            else if (message.StartsWith("PARTIDASG/"))
            {
                // Mensaje de lista de partidas ganadas
                string partidasGanadas = message.Substring(10); // Quitar "PARTIDASG/"
                Debug.WriteLine("[PARTIDAS GANADAS] Lista de partidas ganadas recibida");

                // Actualizar la lista de partidas ganadas
                ListaPartidasGanadasPanel(true, partidasGanadas);
                return;
            }
            else if (message.StartsWith("ERROR/"))
            {
                string errorMsg = message.Substring(6);
                if (message.Contains("ERROR/No es tu turno para pasar"))
                {
                    return;
                }

                Debug.WriteLine($"[ERROR] {errorMsg}");

                // Mostrar mensaje de error al usuario
                GeonBit.UI.Utils.MessageBox.ShowMsgBox(
                    errorMsg,
                    "Error"
                );
            }
            else
            {
                Debug.WriteLine($"[RED] Mensaje no reconocido: {message}");
            }
        }

        private void ActualizarEstadoLider()
        {
            // Ejecutar en el hilo principal
            if (UserInterface.Active != null && UserInterface.Active.Root != null)
            {
                // **ACTUALIZAR BOTÓN PLAY**
                Button playButton = UserInterface.Active.Root.Find<Button>("playBtn");
                if (playButton != null)
                {
                    if (esLider)
                    {
                        playButton.FillColor = Color.Green;
                        playButton.Enabled = true;
                    }
                    else
                    {
                        playButton.FillColor = Color.Gray;
                        playButton.Enabled = false;
                    }
                }

                // **ACTUALIZAR INFORMACIÓN EN EL PANEL DEL GRUPO**
                ActualizarPanelInfoGrupo();

                // **MANTENER EL PANEL DE INFORMACIÓN EXISTENTE (ESQUINA INFERIOR)**
                Panel infoPanel = UserInterface.Active.Root.Find<Panel>("infoPanel");
                if (infoPanel == null)
                {
                    infoPanel = new Panel(new Vector2(200, 50), PanelSkin.Simple, Anchor.BottomLeft);
                    infoPanel.Identifier = "infoPanel";
                    UserInterface.Active.Root.AddChild(infoPanel);
                }

                infoPanel.ClearChildren();
                Paragraph statusText = new Paragraph(esLider ? "LÍDER" : "MIEMBRO");
                statusText.FillColor = esLider ? Color.Green : Color.White;
                statusText.Scale = 0.8f;
                infoPanel.AddChild(statusText);
            }
        }

        private void ActualizarPanelInfoGrupo()
        {
            try
            {
                if (infoGrupoPanel != null && labelJugadores != null)
                {
                    // **ACTUALIZAR LISTA DE JUGADORES**
                    if (jugadoresGrupo.Count > 0)
                    {
                        string textoJugadores = $"Jugadores en el grupo ({jugadoresGrupo.Count}):\n";

                        for (int i = 0; i < jugadoresGrupo.Count; i++)
                        {
                            string jugador = jugadoresGrupo[i];

                            if (jugador.Equals(usuario, StringComparison.OrdinalIgnoreCase))
                            {
                                textoJugadores += $"• {jugador} (TÚ)" + (i == 0 ? " [LÍDER]" : "") + "\n";
                            }
                            else
                            {
                                textoJugadores += $"• {jugador}" + (i == 0 ? " [LÍDER]" : "") + "\n";
                            }
                        }

                        labelJugadores.Text = textoJugadores.TrimEnd();
                        labelJugadores.FillColor = Color.LightGreen;
                    }
                    else
                    {
                        labelJugadores.Text = "No hay información del grupo disponible.\nUsa 'Friends' para unirte a un grupo.";
                        labelJugadores.FillColor = Color.Orange;
                    }

                    // **ACTUALIZAR LABEL DE LÍDER**
                    Paragraph labelLider = infoGrupoPanel.Find<Paragraph>("LabelLider");
                    if (labelLider != null)
                    {
                        labelLider.Text = esLider ? "Eres el LÍDER del grupo" : "Eres MIEMBRO del grupo";
                        labelLider.FillColor = esLider ? Color.Green : Color.Yellow;
                    }

                    Debug.WriteLine($"[GRUPO UI] Panel actualizado - {jugadoresGrupo.Count} jugadores, Líder: {esLider}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error actualizando panel de grupo: {ex.Message}");
            }
        }

        private bool messageListenerRunning = false;
        private void StartMessageListener()
        {
            // Evitar iniciar múltiples hilos de escucha
            if (messageListenerRunning)
            {
                Debug.WriteLine("Hilo de escucha ya está en ejecución, ignorando llamada");
                return;
            }

            stopMessageListener = false;
            messageListenerRunning = true;

            Debug.WriteLine($"Iniciando hilo de escucha. Conectado: {conectado}, Socket válido: {(server != null && server.Connected)}");

            messageListenerThread = new Thread(() =>
            {
                try
                {
                    while (!stopMessageListener)
                    {
                        if (!conectado || server == null || !server.Connected)
                        {
                            Debug.WriteLine("Hilo de escucha detectó desconexión, terminando...");
                            break;
                        }

                        try
                        {
                            byte[] buffer = new byte[1024];

                            // Usar Receive con un timeout para poder comprobar stopMessageListener periódicamente
                            server.ReceiveTimeout = 1000; // 1 segundo
                            int bytesReceived = server.Receive(buffer);

                            if (bytesReceived > 0)
                            {
                                string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                                Debug.WriteLine("Mensaje recibido del servidor: " + message);
                                ProcessServerMessage(message);
                            }
                        }
                        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            // Timeout normal, solo para comprobar stopMessageListener
                            continue;
                        }
                        catch (SocketException ex)
                        {
                            // Solo intentar reconectar si no estamos intentando detener el hilo
                            if (!stopMessageListener && conectado)
                            {
                                Debug.WriteLine("Error en el hilo de mensajes (SocketException): " + ex.Message);
                                conectado = false;
                                ReconnectToServer();
                            }
                            else
                            {
                                Debug.WriteLine("Socket cerrado durante desconexión controlada.");
                                break;
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // Socket ya cerrado, terminar hilo
                            Debug.WriteLine("Socket ya ha sido cerrado.");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error general en el hilo de mensajes: " + ex.Message);
                        }
                    }
                }
                finally
                {
                    messageListenerRunning = false;
                    Debug.WriteLine("El hilo de mensajes se ha detenido correctamente.");
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
            try
            {
                Debug.WriteLine("[SOCKET] Recibiendo socket existente del GameScreen");

                // **DETENER CUALQUIER HILO ANTERIOR**
                stopMessageListener = true;
                if (messageListenerThread != null && messageListenerThread.IsAlive)
                {
                    messageListenerThread.Join(1000); // Esperar máximo 1 segundo
                }

                // **CONFIGURAR EL SOCKET EXISTENTE**
                this.server = existingSocket;
                this.conectado = true;

                Debug.WriteLine($"[SOCKET] Socket configurado. Conectado: {conectado}, Válido: {server != null && server.Connected}");

                // **REINICIAR EL HILO DE ESCUCHA INMEDIATAMENTE**
                messageListenerRunning = false; // Resetear flag
                StartMessageListener();

                // **ENVIAR ESTADO CONECTADO**
                int estadoResult = estado(usuario, "1");
                if (estadoResult == 0)
                {
                    Debug.WriteLine("[SOCKET] Estado 'conectado' enviado correctamente");
                }

                // **SOLICITAR INFORMACIÓN DE SALA/GRUPO**
                SolicitarInformacionSala();
                SolicitarEstadoLider();

                Debug.WriteLine("[SOCKET] Socket existente configurado y hilo de escucha reiniciado");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error configurando socket existente: {ex.Message}");

                // **FALLBACK: CREAR NUEVA CONEXIÓN**
                conectado = false;
                server = null;
                ConnectToServer();
            }
        }
    }
}
