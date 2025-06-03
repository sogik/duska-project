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
using MonoGame.Extended.Particles.Profiles;
using System.Linq;

namespace Duska.Screens
{
    public class GameCardScreen : GameScreen
    {
        private Texture2D[] _cartas = new Texture2D[5];
        private Texture2D _cartaSeleccionada;
        private Texture2D _background;
        private bool _mostrarTodas = true;
        private Random _random = new Random();
        private SpriteBatch _spriteBatch;
        private MonoGame.Extended.Input.KeyboardStateExtended _estadoTecladoAnterior;
        private List<Texture2D> _cartasDisponibles = new List<Texture2D>();
        private BuiltinThemes _currTheme;
        private bool cartasPedidasEstaRonda = false;
        private int rondaActual = 0;
        private Socket server;
        private Thread messageListenerThread;

        private bool isReconnecting = false;
        private volatile bool stopMessageListener = false;
        private bool conectado = false;

        public string usuario;

        private bool estoyEliminado = false;
        private bool partidaTerminada = false;

        private string carta_ronda_actual; // Inicialmente no hay carta jugada

        private string[] cartas_jugadas = new string[5];

        private RichParagraph panelmensajes = new RichParagraph(@"");

        // Añade estas variables para manejar mensajes pendientes
        private List<string> mensajesPendientes = new List<string>();
        private object mensajesLock = new object();
        private bool hayNuevosMensajes = false;

        // Variable para controlar la frecuencia de los mensajes de depuración
        private int _drawCounter = 0;

        // Añade esta variable de clase para mantener el historial de mensajes
        private List<string> historialChat = new List<string>();

        // Añade estas variables a la clase GameCardScreen
        private bool _cartasAcercadas = false;
        private float _escalaCartasNormal = 0.6f;  // Escala reducida para las cartas normales
        private float _escalaCartasAcercadas = 1.0f;  // Escala completa para cuando se acercan
        private Vector2 _posicionCartasNormal;  // Se calculará en LoadContent
        private Vector2 _posicionCartasAcercadas;  // Se calculará en LoadContent
        private int _cartaSeleccionadaIndex = -1;  // Índice de la carta seleccionada con las flechas
        private List<bool> _cartasConFiltro = new List<bool>(); // Estado del filtro por carta
        private Color _colorFiltro = new Color(0, 0, 0, 128); // Color de filtro semitransparente (puedes ajustar los valores)

        private string jugadorConTurnoActual = "";
        private bool esMiTurno = false;
        private bool _permitirAccionesJuego = false; // Fuente para dibujar texto

        // Variables para el sistema de desafío
        private bool mostrandoDesafio = false;
        private float tiempoDesafio = 0f;
        private const float DURACION_DESAFIO = 3.0f; // 5 segundos de duración
        private List<Texture2D> cartasDesafio = new List<Texture2D>();
        private List<bool> cartasDesafioValidas = new List<bool>(); // true = verde, false = roj

        public GameCardScreen(Game game, string usuario) : base(game)
        {
            this.usuario = usuario;
            _cartasDisponibles = new List<Texture2D>();
            _cartas = new Texture2D[5];
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

                _background = Content.Load<Texture2D>("bg2");

                // Cargar texturas de cartas
                string[] cartasNombres = new string[] { "ace", "jack", "king", "queen", "cardback" };
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
                    // Añade esta línea después de cargar cada textura para verificar si es nula
                    Debug.WriteLine($"Carta {cartasNombres[i]} estado: {(_cartas[i] != null ? "OK" : "NULA")}");
                }

                // Inicializar con cartas de muestra
                _cartasDisponibles.Clear();
                _cartasDisponibles.Add(_cartas[4]);
                _cartasDisponibles.Add(_cartas[4]);
                _cartasDisponibles.Add(_cartas[4]);
                _cartasDisponibles.Add(_cartas[4]);
                Debug.WriteLine($"[INIT] Cartas de muestra añadidas: {_cartasDisponibles.Count}");

                // Conectar al servidor y solicitar cartas (solo una vez)
                if (conectado || ConnectToServerIfNeeded())
                {
                    MensajesPrueba();
                    StartMessageListener();
                    Thread.Sleep(300); // Esperar 300 ms
                    PedirTurno();
                    obtener_carta_ronda();
                    //GetCards(usuario);
                    ChatPanel(true, "Bienvenido al juego, " + usuario);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error general en LoadContent: {ex.Message}");
            }

            _posicionCartasNormal = new Vector2(
                GraphicsDevice.Viewport.Width / 2 - (_cartasDisponibles.Count * 60),
                GraphicsDevice.Viewport.Height - 150);

            _posicionCartasAcercadas = new Vector2(
                GraphicsDevice.Viewport.Width / 2 - (_cartasDisponibles.Count * 90),
                GraphicsDevice.Viewport.Height / 2 - 112);

            Debug.WriteLine($"[INIT] Posición normal cartas: {_posicionCartasNormal}, Posición acercada: {_posicionCartasAcercadas}");
        }

        private void PedirTurno()
        {
            if (server != null && server.Connected)
            {
                string mensaje = "23/" + usuario;
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);
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

            // Añadir nuevo botón para abandonar partida
            Button abandonarBtn = new Button("Abandonar Partida", ButtonSkin.Default);
            abandonarBtn.OnClick = (Entity btn) =>
            {
                // Cerrar el menú de pausa
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);

                // Mostrar el panel de confirmación personalizado
                MostrarPanelConfirmacionAbandono();
            };
            panel.AddChild(abandonarBtn);
        }

        private void MostrarPanelConfirmacionAbandono()
        {
            // Crear el panel principal
            Panel panel = new Panel(new Vector2(450, -1), PanelSkin.Default, Anchor.Center);
            panel.Visible = true;
            UserInterface.Active.AddEntity(panel);

            // Título y línea decorativa
            panel.AddChild(new Header("Abandonar Partida"));
            panel.AddChild(new HorizontalLine());

            // Mensaje de confirmación
            panel.AddChild(new Paragraph("¿Estás seguro que quieres abandonar la partida?"));
            panel.AddChild(new HorizontalLine());

            // Botón para confirmar abandono
            Button acceptBtn = new Button("Sí", ButtonSkin.Default);
            acceptBtn.OnClick = (Entity btn) =>
            {
                try
                {
                    // IMPORTANTE: Detener el hilo de escucha ANTES de enviar el mensaje
                    stopMessageListener = true;

                    if (server != null && server.Connected)
                    {
                        // Enviar mensaje para abandonar partida
                        string mensaje = "27/" + usuario;
                        byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                        server.Send(msg);

                        Debug.WriteLine("[ABANDONO] Solicitud enviada para abandonar partida");
                    }

                    // LIMPIAR COMPLETAMENTE LA INTERFAZ DE USUARIO
                    if (UserInterface.Active != null)
                    {
                        UserInterface.Active.Clear();
                        Debug.WriteLine("[UI] Interfaz de usuario limpiada antes de cambiar pantalla");
                    }

                    // Esperar un momento para que se procese el mensaje
                    System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
                    {
                        // Crear nueva instancia del menú principal con socket limpio
                        var mainMenuScreen = new MainMenuScreen(Game, usuario);

                        // IMPORTANTE: No pasar el socket existente para que se cree una nueva conexión
                        // mainMenuScreen.SetExistingSocket(server); // NO hacer esto

                        // Cambiar a la pantalla del menú principal
                        ScreenManager.LoadScreen(mainMenuScreen, new FadeTransition(GraphicsDevice, Color.Black, 0.5f));
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Error al abandonar partida: {ex.Message}");

                    // Asegurar limpieza incluso si hay error
                    if (UserInterface.Active != null)
                    {
                        UserInterface.Active.Clear();
                    }

                    // Ir al menú principal de todas formas
                    var mainMenuScreen = new MainMenuScreen(Game, usuario);
                    ScreenManager.LoadScreen(mainMenuScreen, new FadeTransition(GraphicsDevice, Color.Black, 0.5f));
                }
            };
            panel.AddChild(acceptBtn);

            // Botón para cancelar
            Button declineBtn = new Button("No", ButtonSkin.Default);
            declineBtn.OnClick = (Entity btn) =>
            {
                // Simplemente cerrar el panel
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
            };
            panel.AddChild(declineBtn);
        }

        private void ControlarAccionesPorTurno(bool permitir)
        {
            _permitirAccionesJuego = permitir;

            Debug.WriteLine($"[TURNOS] Acciones de juego {(permitir ? "HABILITADAS" : "BLOQUEADAS")}");
        }

        private string GetTipoCartaPorTextura(Texture2D textura)
        {
            // **DEVOLVER NOMBRES EXACTOS QUE ESPERA EL SERVIDOR**
            if (textura == _cartas[0]) // ace
                return "ace";
            else if (textura == _cartas[1]) // jack  
                return "jack";
            else if (textura == _cartas[2]) // king
                return "king";
            else if (textura == _cartas[3]) // queen
                return "queen";
            else if (textura == _cartas[4]) // cardback
                return "cardback";
            else
                return "desconocida";
        }

        // Añadir este nuevo método a GameCardScreen:

        private void ActualizarSoloPanelMensajes()
        {
            try
            {
                Debug.WriteLine("[CHAT] *** INICIANDO ACTUALIZACIÓN DE PANEL MENSAJES ***");

                // **BUSCAR EL PANEL DE MENSAJES EXISTENTE**
                Panel panelPrincipal = null;
                Panel panelMensajes = null;

                if (UserInterface.Active != null && UserInterface.Active.Root.Children.Count > 0)
                {
                    // Buscar el panel principal de chat
                    foreach (var entity in UserInterface.Active.Root.Children)
                    {
                        if (entity is Panel panel && panel.Anchor == Anchor.TopRight)
                        {
                            panelPrincipal = panel;
                            Debug.WriteLine("[CHAT] Panel principal encontrado");
                            break;
                        }
                    }

                    // Si encontramos el panel principal, buscar el panel de mensajes dentro
                    if (panelPrincipal != null)
                    {
                        foreach (var child in panelPrincipal.Children)
                        {
                            if (child is Panel p && p.Skin == PanelSkin.ListBackground)
                            {
                                panelMensajes = p;
                                Debug.WriteLine("[CHAT] Panel de mensajes encontrado");
                                break;
                            }
                        }
                    }
                }

                // **SI NO EXISTE EL PANEL O NO LO ENCONTRAMOS, RECREAR TODO**
                if (panelPrincipal == null || panelMensajes == null)
                {
                    Debug.WriteLine("[CHAT] Panel no encontrado, recreando interfaz completa");
                    CrearInterfazChatCompleta(true);
                    return;
                }

                // **ACTUALIZAR SOLO EL CONTENIDO DEL PANEL DE MENSAJES**
                Debug.WriteLine("[CHAT] Actualizando contenido del panel existente");
                panelMensajes.ClearChildren();

                // Recalcular mensajes por pantalla
                int altoPanelMensajes = panelMensajes.Size.Y > 0 ? (int)panelMensajes.Size.Y : 300;
                int mensajesPorPantalla = Math.Max(10, (altoPanelMensajes - 20) / 25);
                int totalMensajes = historialChat?.Count ?? 0;
                int indiceInicio = Math.Max(0, totalMensajes - mensajesPorPantalla);

                Debug.WriteLine($"[CHAT] Total mensajes: {totalMensajes}, Mostrando desde: {indiceInicio}");

                // Añadir mensajes actualizados
                if (historialChat != null && totalMensajes > 0)
                {
                    for (int i = indiceInicio; i < totalMensajes; i++)
                    {
                        if (i >= 0 && i < historialChat.Count)
                        {
                            string msg = historialChat[i];
                            if (!string.IsNullOrEmpty(msg))
                            {
                                Paragraph mensajeParagraph = new Paragraph(msg);
                                mensajeParagraph.Scale = 0.8f;
                                mensajeParagraph.WrapWords = true;

                                // **COLOREAR MENSAJES**
                                if (msg.StartsWith($"{usuario}:"))
                                {
                                    mensajeParagraph.FillColor = Color.LightCyan;
                                }
                                else if (msg.StartsWith("Sistema:"))
                                {
                                    mensajeParagraph.FillColor = Color.Yellow;
                                }
                                else
                                {
                                    mensajeParagraph.FillColor = Color.White;
                                }

                                panelMensajes.AddChild(mensajeParagraph);
                                Debug.WriteLine($"[CHAT] Mensaje añadido: {msg.Substring(0, Math.Min(30, msg.Length))}...");
                            }
                        }
                    }
                }
                else
                {
                    Paragraph noMensajes = new Paragraph("No hay mensajes aún...");
                    noMensajes.FillColor = Color.Gray;
                    noMensajes.Scale = 0.8f;
                    panelMensajes.AddChild(noMensajes);
                    Debug.WriteLine("[CHAT] Mensaje placeholder añadido");
                }

                Debug.WriteLine("[CHAT] *** PANEL DE MENSAJES ACTUALIZADO EXITOSAMENTE ***");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error actualizando panel mensajes: {ex.Message}");
                // Fallback: recrear toda la interfaz
                try
                {
                    CrearInterfazChatCompleta(true);
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"[ERROR] Error crítico en fallback: {ex2.Message}");
                }
            }
        }

        private void ProcessServerMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                message = message.Trim();
                Debug.WriteLine($"[SERVER] Procesando mensaje: {message}");

                if (message.StartsWith("CARDS/"))
                {
                    Debug.WriteLine($"[SERVER] *** MENSAJE DE CARTAS RECIBIDO *** : {message}");
                    PedirTurno();

                    // Solo procesa o encola, pero no ambos
                    if (System.Threading.Thread.CurrentThread.IsBackground)
                    {
                        // Si estamos en un hilo background, encola para procesamiento en hilo principal
                        lock (mensajesLock)
                        {
                            mensajesPendientes.Add(message);
                            hayNuevosMensajes = true;
                            Debug.WriteLine("[SERVER] Mensaje añadido a la cola para procesamiento");
                        }
                    }
                    else
                    {
                        // Si estamos en el hilo principal, procesa directamente
                        ProcesarCartasDelServidor(message);
                        // Forzar actualización inmediata
                        _mostrarTodas = true;
                    }
                    return;
                }
                // En ProcessServerMessage, cambiar la sección de CHAT/:

                else if (message.StartsWith("CHAT/"))
                {
                    string chatMessage = message.Substring(5).Trim();
                    Debug.WriteLine("Mensaje de chat recibido: " + chatMessage);

                    string[] partes = chatMessage.Split('/');
                    if (partes.Length >= 2)
                    {
                        string usuarioChat = partes[0];
                        string mensajeTexto = partes[1];

                        // **AÑADIR INMEDIATAMENTE AL HISTORIAL**
                        historialChat.Add($"{usuarioChat}: {mensajeTexto}");

                        // **ACTUALIZAR INMEDIATAMENTE INDEPENDIENTEMENTE DEL HILO**
                        if (System.Threading.Thread.CurrentThread.IsBackground)
                        {
                            // **ENCOLAR ACTUALIZACIÓN DE CHAT PARA EL HILO PRINCIPAL**
                            lock (mensajesLock)
                            {
                                mensajesPendientes.Add("ACTUALIZAR_CHAT");
                                hayNuevosMensajes = true;
                            }
                            Debug.WriteLine("[CHAT] Actualización de chat encolada para hilo principal");
                        }
                        else
                        {
                            // Si estamos en hilo principal, actualizar inmediatamente
                            ActualizarSoloPanelMensajes();
                            Debug.WriteLine("[CHAT] Chat actualizado inmediatamente en hilo principal");
                        }

                        Debug.WriteLine($"[CHAT] Mensaje añadido: {usuarioChat}: {mensajeTexto}");
                    }
                    return;
                }
                else if (message.StartsWith("TURN/"))
                {
                    string jugadorTurno = message.Substring(5).Trim();

                    // **EVITAR SPAM DE TURNOS - SOLO PROCESAR SI CAMBIÓ**
                    if (jugadorTurno != jugadorConTurnoActual)
                    {
                        esMiTurno = string.Equals(jugadorTurno, usuario, StringComparison.OrdinalIgnoreCase);
                        _permitirAccionesJuego = esMiTurno;
                        jugadorConTurnoActual = jugadorTurno;

                        // **RECREAR UI PARA MOSTRAR CAMBIO DE TURNO**
                        CrearInterfazChatCompleta(true);

                        Debug.WriteLine($"[TURNOS] Cambio de turno: {jugadorConTurnoActual} | ¿Es mi turno? {esMiTurno}");
                    }
                    return;
                }
                else if (message.StartsWith("ACTION/"))
                {
                    // Formato esperado: ACTION/jugador/accion/datos
                    string[] parts = message.Substring(7).Split('/');

                    if (parts.Length >= 3)
                    {
                        string jugador = parts[0];
                        string accion = parts[1];
                        string datos = parts.Length > 2 ? parts[2] : "";

                        // Mostrar la acción en el chat
                        if (accion.ToUpper() == "PLAY")
                        {
                            // Parsear el formato especial para cartas: cantidad,tipo1,tipo2,...
                            string[] cartasInfo = datos.Split(',');
                            if (cartasInfo.Length > 0)
                            {
                                int cantidad = int.Parse(cartasInfo[0]);
                                ChatPanel(true, $"Sistema/{jugador} ha jugado {cantidad} carta(s)");

                                // Mostrar los tipos de cartas si hay datos disponibles
                                if (cartasInfo.Length > 1)
                                {
                                    string tiposMsg = "Tipos: ";
                                    for (int i = 1; i < cartasInfo.Length; i++)
                                    {
                                        tiposMsg += cartasInfo[i];
                                        if (i < cartasInfo.Length - 1)
                                            tiposMsg += ", ";
                                    }
                                    ChatPanel(true, $"Sistema/{tiposMsg}");
                                }
                            }
                            else
                            {
                                // Mensaje genérico si no hay detalles
                                ChatPanel(true, $"Sistema/{jugador} ha jugado cartas: {datos}");
                            }
                        }
                        else
                        {
                            // Para otras acciones, mensaje genérico
                            ChatPanel(true, $"Sistema/{jugador} ha realizado: {accion} {datos}");
                        }
                    }
                    return;
                }
                else if (message.StartsWith("DESAFIO/"))
                {
                    string mensaje = message.Substring(8).Trim(); // mensaje = "EXITO/carta1/carta2" o "FALLIDO/carta1/carta2"

                    if (mensaje.StartsWith("EXITO"))
                    {
                        string[] partes = mensaje.Split('/');
                        // partes[0] = "EXITO"
                        // partes[1..n] = cartas jugadas

                        // Extraer las cartas para mostrar
                        List<string> cartas = new List<string>();
                        for (int i = 1; i < partes.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(partes[i].Trim()))
                            {
                                cartas.Add(partes[i].Trim());
                            }
                        }

                        // Usar TU función existente
                        PrepararCartasDesafio(cartas.ToArray());

                        ChatPanel(true, $"Sistema/¡Desafío exitoso! El jugador desafiado mintió y será eliminado.");
                        ChatPanel(true, $"Sistema/Cartas reveladas: {string.Join(", ", cartas)}");
                    }
                    else if (mensaje.StartsWith("FALLIDO"))
                    {
                        string[] partes = mensaje.Split('/');

                        // Extraer las cartas para mostrar
                        List<string> cartas = new List<string>();
                        for (int i = 1; i < partes.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(partes[i].Trim()))
                            {
                                cartas.Add(partes[i].Trim());
                            }
                        }

                        // Usar TU función existente
                        PrepararCartasDesafio(cartas.ToArray());

                        ChatPanel(true, $"Sistema/Desafío fallido. Las cartas eran válidas y el desafiante será eliminado.");
                        ChatPanel(true, $"Sistema/Cartas reveladas: {string.Join(", ", cartas)}");
                    }
                    return;
                }
                if (message.StartsWith("CARTA_RONDA"))
                {
                    string carta = message.Substring(12).Trim();
                    carta_ronda_actual = carta;

                    // **MOSTRAR MENSAJE AL USUARIO**
                    ChatPanel(true, $"Sistema/Carta de la ronda: {carta_ronda_actual}");
                    Debug.WriteLine($"[JUEGO] Carta de la ronda actual: {carta_ronda_actual}");
                    return;
                }
                else if (message.StartsWith("NUEVA_RONDA/"))
                {
                    Debug.WriteLine("[NUEVA_RONDA] *** MENSAJE DE NUEVA RONDA RECIBIDO ***");

                    string[] partes = message.Split('/');
                    if (partes.Length >= 2)
                    {
                        string numeroRonda = partes[1];

                        // **RESETEAR FLAGS DE CARTAS**
                        cartasPedidasEstaRonda = false;
                        rondaActual = int.TryParse(numeroRonda, out int ronda) ? ronda : rondaActual + 1;

                        // **SI HAY CARTA EN EL MENSAJE, USARLA**
                        if (partes.Length >= 3)
                        {
                            carta_ronda_actual = partes[2];
                            Debug.WriteLine($"[NUEVA_RONDA] Carta recibida en mensaje: {carta_ronda_actual}");
                        }
                        else
                        {
                            // **FALLBACK: OBTENER CARTA DEL SERVIDOR**
                            obtener_carta_ronda();
                        }

                        // **NOTIFICAR Y RECREAR UI**
                        historialChat.Add($"Sistema: ¡Comienza la ronda {numeroRonda}! Carta designada: {carta_ronda_actual}");

                        // **SOLICITAR CARTAS PARA LA NUEVA RONDA**
                        if (conectado || ConnectToServerIfNeeded())
                        {
                            GetCards(usuario);
                            cartasPedidasEstaRonda = true;
                            PedirTurno();
                            Debug.WriteLine($"[NUEVA_RONDA] Cartas solicitadas para ronda {numeroRonda}");
                        }

                        // **ENCOLAR ACTUALIZACIÓN DE UI**
                        lock (mensajesLock)
                        {
                            mensajesPendientes.Add("ACTUALIZAR_CHAT");
                            hayNuevosMensajes = true;
                        }
                    }
                    return;
                }
                else if (message.StartsWith("JUGADOR_ELIMINADO/"))
                {
                    Debug.WriteLine("[ELIMINACIÓN] *** MENSAJE DE ELIMINACIÓN RECIBIDO ***");

                    string[] partes = message.Split('/');
                    if (partes.Length >= 2)
                    {
                        string jugadorEliminado = partes[1].Trim();

                        Debug.WriteLine($"[ELIMINACIÓN] Jugador eliminado: '{jugadorEliminado}'");
                        Debug.WriteLine($"[ELIMINACIÓN] Mi usuario: '{usuario}'");

                        bool soyYo = string.Equals(jugadorEliminado, usuario, StringComparison.OrdinalIgnoreCase);

                        if (soyYo)
                        {
                            Debug.WriteLine("[ELIMINACIÓN] *** SOY YO EL ELIMINADO ***");
                            estoyEliminado = true;
                            _permitirAccionesJuego = false;
                            esMiTurno = false;

                            // **FORZAR MOSTRADO INMEDIATO DEL PANEL**
                            lock (mensajesLock)
                            {
                                // **LIMPIAR MENSAJES PREVIOS Y AÑADIR SOLO EL CRÍTICO**
                                mensajesPendientes.Clear();
                                mensajesPendientes.Add("MOSTRAR_PANEL_ELIMINADO_GEONBIT");
                                hayNuevosMensajes = true;
                            }
                            Debug.WriteLine("[ELIMINACIÓN] Panel Geonbit de eliminación encolado con prioridad");
                        }
                        else
                        {
                            historialChat.Add($"Sistema: {jugadorEliminado} ha sido eliminado de la partida");

                            lock (mensajesLock)
                            {
                                mensajesPendientes.Add("ACTUALIZAR_CHAT");
                                hayNuevosMensajes = true;
                            }
                        }
                    }
                    return;
                }
                else if (message.StartsWith("FIN_PARTIDA/"))
                {
                    Debug.WriteLine("[FIN_PARTIDA] *** MENSAJE FIN PARTIDA RECIBIDO ***");

                    string[] partes = message.Split('/');
                    if (partes.Length >= 2)
                    {
                        string ganador = partes[1].Trim();

                        partidaTerminada = true;
                        _permitirAccionesJuego = false;
                        esMiTurno = false;

                        Debug.WriteLine($"[FIN_PARTIDA] *** PARTIDA TERMINADA - GANADOR: {ganador} ***");

                        // **AÑADIR MENSAJE AL CHAT**
                        if (ganador.Equals(usuario, StringComparison.OrdinalIgnoreCase))
                        {
                            historialChat.Add("Sistema: ¡FELICIDADES! ¡HAS GANADO LA PARTIDA!");
                        }
                        else
                        {
                            historialChat.Add($"Sistema: Fin de partida. Ganador: {ganador}");
                        }

                        // **FORZAR MOSTRADO INMEDIATO DEL PANEL**
                        lock (mensajesLock)
                        {
                            // **LIMPIAR MENSAJES PREVIOS Y AÑADIR SOLO EL CRÍTICO**
                            mensajesPendientes.Clear();
                            mensajesPendientes.Add($"MOSTRAR_PANEL_FIN_PARTIDA/{ganador}");
                            hayNuevosMensajes = true;
                        }
                        Debug.WriteLine("[FIN_PARTIDA] Panel encolado con prioridad para mostrar en hilo principal");
                    }
                    return;
                }
                else if (message.StartsWith("GRUPO_DISUELTO/"))
                {
                    ChatPanel(true, "Sistema/El grupo ha sido disuelto. Regresando al menú principal...");

                    // Esperar un momento y regresar al menú principal
                    System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
                    {
                        RegresarAlMenuPrincipal();
                    });
                    return;
                }
                else if (message.StartsWith("PARTIDA_CANCELADA/"))
                {
                    string razon = message.Substring(17).Trim();
                    partidaTerminada = true;
                    _permitirAccionesJuego = false;

                    ChatPanel(true, $"Sistema/Partida cancelada: {razon}");

                    // PROCESAR EN EL HILO PRINCIPAL
                    if (System.Threading.Thread.CurrentThread.IsBackground)
                    {
                        lock (mensajesLock)
                        {
                            mensajesPendientes.Add($"MOSTRAR_PANEL_CANCELADA/{razon}");
                            hayNuevosMensajes = true;
                            Debug.WriteLine("[PARTIDA_CANCELADA] Panel encolado para mostrar en hilo principal");
                        }
                    }
                    else
                    {
                        MostrarPanelPartidaCancelada(razon);
                    }
                    return;
                }
                else if (message.StartsWith("JUGADOR_ABANDONO/"))
                {
                    string jugadorQueAbandono = message.Substring(16).Trim();

                    ChatPanel(true, $"Sistema/El jugador {jugadorQueAbandono} ha abandonado la partida");

                    // Si era el que tenía el turno, el turno debería cambiar automáticamente
                    if (jugadorQueAbandono.Equals(jugadorConTurnoActual, StringComparison.OrdinalIgnoreCase))
                    {
                        ChatPanel(true, "Sistema/Esperando nuevo turno...");
                    }

                    return;
                }
                else if (message.StartsWith("ERROR/"))
                {
                    // Mensaje de error del servidor
                    string errorMessage = message.Substring(6).Trim();
                    Debug.WriteLine($"[SERVER] Error recibido: {errorMessage}");
                    ChatPanel(true, $"Sistema/Error: {errorMessage}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVER] Error procesando mensaje: {ex.Message}");
            }
        }

        private void MostrarPanelPartidaCancelada(string razon)
        {
            try
            {
                Debug.WriteLine("[UI] *** INICIANDO MostrarPanelPartidaCancelada ***");

                // LIMPIAR UI COMPLETAMENTE
                if (UserInterface.Active != null)
                {
                    UserInterface.Active.Clear();
                }
                else
                {
                    Debug.WriteLine("[ERROR] UserInterface.Active es null");
                    return;
                }

                // Crear panel principal
                Panel panel = new Panel(new Vector2(400, 250), PanelSkin.Default, Anchor.Center);
                panel.Visible = true;

                // Título
                Header titulo = new Header("PARTIDA CANCELADA");
                titulo.FillColor = Color.Orange;
                panel.AddChild(titulo);

                panel.AddChild(new HorizontalLine());

                // Mensaje
                panel.AddChild(new Paragraph("La partida ha sido cancelada"));
                panel.AddChild(new Paragraph($"Razón: {razon}"));
                panel.AddChild(new HorizontalLine());

                // Botón para regresar al menú
                Button regresarBtn = new Button("Regresar al Menú", ButtonSkin.Default);
                regresarBtn.Size = new Vector2(200, 50);
                regresarBtn.FillColor = Color.LightBlue;
                regresarBtn.OnClick = (Entity btn) =>
                {
                    Debug.WriteLine("[PARTIDA_CANCELADA] Botón regresar clickeado");
                    RegresarAlMenuPrincipal();
                };
                panel.AddChild(regresarBtn);

                // Añadir al UserInterface
                UserInterface.Active.AddEntity(panel);

                Debug.WriteLine("[UI] *** Panel partida cancelada creado exitosamente ***");

                // Auto-regresar después de 10 segundos
                // En MostrarPanelPartidaCancelada, cambiar el auto-regreso:

                // **AUTO-REGRESAR DESPUÉS DE 15 SEGUNDOS**
                System.Threading.Tasks.Task.Delay(15000).ContinueWith(_ =>
                {
                    if (panel.Visible)
                    {
                        Debug.WriteLine("[PARTIDA_CANCELADA] Auto-regreso al menú después de 15 segundos");
                        RegresarAlMenuPrincipal();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en MostrarPanelPartidaCancelada: {ex.Message}");

                // Plan de respaldo
                ChatPanel(true, "Sistema/Partida cancelada. Regresando al menú principal en 3 segundos...");
                System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
                {
                    RegresarAlMenuPrincipal();
                });
            }
        }

        private void InicializarUIJuego()
        {
            try
            {
                Debug.WriteLine("[UI] Regenerando UI del juego...");

                // Recrear elementos básicos de la UI del juego
                // Solo si necesitas mantener elementos visibles como espectador

                // Por ejemplo, recrear el panel de chat:
                Panel chatPanel = new Panel(new Vector2(300, 200), PanelSkin.Default, Anchor.BottomLeft);
                chatPanel.Visible = true;
                UserInterface.Active.AddEntity(chatPanel);

                // Añadir otros elementos de UI que necesites mantener visibles

                Debug.WriteLine("[UI] UI del juego regenerada");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al regenerar UI: {ex.Message}");
            }
        }

        // Agregar este método nuevo a la clase:

        private void MostrarPanelEliminacion()
        {
            // Panel principal centrado - usando solo el tema por defecto
            var panelEliminacion = new GeonBit.UI.Entities.Panel(
                new Vector2(500, 300),
                PanelSkin.Default,
                Anchor.Center
            );

            // Título del panel
            var titulo = new GeonBit.UI.Entities.Header("¡ELIMINADO!");
            titulo.Anchor = Anchor.TopCenter;
            titulo.FillColor = Color.Red;
            panelEliminacion.AddChild(titulo);

            // Mensaje explicativo
            var mensaje = new GeonBit.UI.Entities.Paragraph(
                "Has sido eliminado de la partida.\nLa partida ha terminado para ti."
            );
            mensaje.Anchor = Anchor.Center;
            mensaje.Scale = 0.8f;
            panelEliminacion.AddChild(mensaje);

            // Botón para continuar
            var botonContinuar = new GeonBit.UI.Entities.Button("Regresar al Menú");
            botonContinuar.Anchor = Anchor.BottomCenter;
            botonContinuar.Size = new Vector2(250, 60);

            // Evento del botón
            botonContinuar.OnClick = (GeonBit.UI.Entities.Entity entity) =>
            {
                Debug.WriteLine("[PANEL] Botón presionado - regresando al menú");

                // Remover el panel
                UserInterface.Active.RemoveEntity(panelEliminacion);

                // Regresar al menú principal
                var mainMenuScreen = new MainMenuScreen(Game, usuario);
                ScreenManager.LoadScreen(mainMenuScreen, new FadeTransition(GraphicsDevice, Color.Black));
            };

            panelEliminacion.AddChild(botonContinuar);

            // Agregar el panel a la interfaz
            UserInterface.Active.AddEntity(panelEliminacion);

            Debug.WriteLine("[GEONBIT] Panel de eliminación mostrado");
        }

        // Reemplazar MostrarPanelFinPartida en GameCardScreen.cs:

        private void MostrarPanelFinPartida(string ganador)
        {
            try
            {
                Debug.WriteLine("[UI] *** INICIANDO MostrarPanelFinPartida ***");

                // **LIMPIAR UI COMPLETAMENTE**
                if (UserInterface.Active != null)
                {
                    UserInterface.Active.Clear();
                    Debug.WriteLine("[UI] UI limpiada para mostrar fin de partida");
                }

                // **CREAR PANEL PRINCIPAL**
                Panel panel = new Panel(new Vector2(500, 350), PanelSkin.Default, Anchor.Center);
                panel.FillColor = Color.Black * 0.8f;
                panel.OutlineColor = Color.Gold;
                panel.OutlineWidth = 3;
                panel.Visible = true;

                // **TÍTULO DESTACADO**
                Header titulo = new Header("¡PARTIDA TERMINADA!");
                titulo.FillColor = ganador.Equals(usuario, StringComparison.OrdinalIgnoreCase)
                    ? Color.Gold : Color.Orange;
                titulo.Scale = 1.4f;
                panel.AddChild(titulo);

                panel.AddChild(new HorizontalLine());

                // **MENSAJE DEL GANADOR**
                if (ganador.Equals(usuario, StringComparison.OrdinalIgnoreCase))
                {
                    Paragraph felicidades = new Paragraph("¡FELICIDADES!");
                    felicidades.FillColor = Color.Gold;
                    felicidades.Scale = 1.2f;
                    panel.AddChild(felicidades);

                    Paragraph victoria = new Paragraph("¡HAS GANADO LA PARTIDA!");
                    victoria.FillColor = Color.Yellow;
                    victoria.Scale = 1.1f;
                    panel.AddChild(victoria);
                }
                else
                {
                    panel.AddChild(new Paragraph($"Ganador: {ganador}"));
                    panel.AddChild(new Paragraph("¡Mejor suerte la próxima vez!"));
                }

                panel.AddChild(new HorizontalLine());

                // **BOTÓN PARA REGRESAR**
                Button regresarBtn = new Button("Regresar al Menú", ButtonSkin.Default);
                regresarBtn.Size = new Vector2(220, 60);
                regresarBtn.FillColor = Color.LightGreen;
                regresarBtn.OnClick = (Entity btn) =>
                {
                    Debug.WriteLine("[FIN_PARTIDA] Botón regresar clickeado");
                    RegresarAlMenuPrincipal();
                };
                panel.AddChild(regresarBtn);

                // **AÑADIR AL UI**
                UserInterface.Active.AddEntity(panel);

                Debug.WriteLine("[UI] *** Panel fin de partida creado y añadido exitosamente ***");

                // **AUTO-REGRESAR DESPUÉS DE 15 SEGUNDOS**
                // En MostrarPanelFinPartida, cambiar el auto-regreso:

                // **AUTO-REGRESAR DESPUÉS DE 20 SEGUNDOS EN LUGAR DE 15**
                System.Threading.Tasks.Task.Delay(20000).ContinueWith(_ =>
                {
                    try
                    {
                        if (panel != null && panel.Visible)
                        {
                            Debug.WriteLine("[UI] Auto-regreso después de 20 segundos");
                            RegresarAlMenuPrincipal();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[UI] Error en auto-regreso: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] *** Error crítico en MostrarPanelFinPartida: {ex.Message} ***");

                // **PLAN DE RESPALDO**
                ChatPanel(true, "Sistema/Partida terminada. Regresando al menú principal en 3 segundos...");
                System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
                {
                    RegresarAlMenuPrincipal();
                });
            }
        }

        private void RegresarAlMenuPrincipal()
        {
            try
            {
                Debug.WriteLine("[NAVEGACIÓN] Regresando al menú principal");

                // **NO DETENER EL SOCKET - MANTENERLO VIVO**
                stopMessageListener = true;

                // Limpiar la UI antes de cambiar
                if (UserInterface.Active != null)
                {
                    UserInterface.Active.Clear();
                }

                // **PASAR EL SOCKET EXISTENTE AL MENÚ PRINCIPAL**
                var mainMenuScreen = new MainMenuScreen(Game, usuario);

                // **IMPORTANTE: Pasar el socket vivo para mantener la conexión**
                if (server != null && server.Connected)
                {
                    mainMenuScreen.SetExistingSocket(server);
                    Debug.WriteLine("[NAVEGACIÓN] Socket transferido al menú principal");
                }

                ScreenManager.LoadScreen(mainMenuScreen, new FadeTransition(GraphicsDevice, Color.Black));

                Debug.WriteLine("[NAVEGACIÓN] Cambio de pantalla iniciado");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al regresar al menú: {ex.Message}");
            }
        }

        private void obtener_carta_ronda()
        {
            try
            {
                if (server != null && server.Connected)
                {
                    string mensajeFormato = "26/" + usuario + "/LALA";
                    byte[] msg = Encoding.ASCII.GetBytes(mensajeFormato);
                    server.Send(msg);
                    Debug.WriteLine("[RONDA] Solicitando información de carta de ronda actual");
                }
                else
                {
                    Debug.WriteLine("[ERROR] No se puede solicitar carta de ronda - sin conexión");
                    if (ConnectToServerIfNeeded())
                    {
                        obtener_carta_ronda(); // Reintentar si la reconexión es exitosa
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al solicitar carta de ronda: {ex.Message}");
            }
        }

        private void PrepararCartasDesafio(string[] cartasJugadas)
        {
            // Limpiar listas previas
            cartasDesafio.Clear();
            cartasDesafioValidas.Clear();

            Debug.WriteLine($"[DESAFÍO] Preparando {cartasJugadas.Length} cartas para visualización");

            // **AÑADIR MENSAJE AL CHAT**
            ChatPanel(true, "Sistema/Mostrando resultado del desafío durante 3 segundos...");

            // Para cada carta jugada, determinar si es válida según la carta de ronda
            foreach (string nombreCarta in cartasJugadas)
            {
                if (string.IsNullOrEmpty(nombreCarta)) continue;

                // Cargar la textura adecuada según el nombre
                Texture2D textura = ObtenerTexturaParaCarta(nombreCarta);
                if (textura != null)
                {
                    cartasDesafio.Add(textura);

                    // Verificar si coincide con la carta de ronda
                    bool esValida = EsCartaValidaParaRonda(nombreCarta, carta_ronda_actual);
                    cartasDesafioValidas.Add(esValida);

                    Debug.WriteLine($"[DESAFÍO] Carta {nombreCarta}: {(esValida ? "VÁLIDA (verde)" : "INVÁLIDA (roja)")}");
                }
            }

            // **ACTIVAR LA VISUALIZACIÓN Y REINICIAR EL TEMPORIZADOR**
            mostrandoDesafio = true;
            tiempoDesafio = 0f;

            Debug.WriteLine("[DESAFÍO] Iniciando visualización de 3 segundos");
        }

        // Método para determinar si una carta es válida para la ronda actual
        private bool EsCartaValidaParaRonda(string nombreCarta, string tipoRonda)
        {
            // Ignorar si falta algún dato
            if (string.IsNullOrEmpty(nombreCarta) || string.IsNullOrEmpty(tipoRonda))
                return false;

            // Comparar el tipo de la carta con el tipo de la ronda
            if (tipoRonda == "ACES" && nombreCarta == "ace")
                return true;
            else if (tipoRonda == "REYES" && nombreCarta == "king")
                return true;
            else if (tipoRonda == "REINAS" && nombreCarta == "queen")
                return true;
            else if (tipoRonda == "JOKERS" && nombreCarta == "jack")
                return true;

            return false;
        }

        // Obtener la textura correspondiente al nombre de la carta
        private Texture2D ObtenerTexturaParaCarta(string nombreCarta)
        {
            switch (nombreCarta.ToLower())
            {
                case "ace": return _cartas[0];
                case "jack": return _cartas[1];
                case "king": return _cartas[2];
                case "queen": return _cartas[3];
                default: return null;
            }
        }

        private void ProcesarAccionJugador(string jugador, string accion, string datos)
        {
            // Simplemente mostrar un mensaje en el chat
            ChatPanel(true, $"Sistema/{jugador} ha realizado: {accion} {datos}");
            Debug.WriteLine($"[JUEGO] Acción: {jugador} realizó {accion} con datos: {datos}");
        }

        // En LoadContent o cuando se reciben nuevas cartas:
        private void ProcesarCartasDelServidor(string cartasString)
        {
            Debug.WriteLine($"[CARTAS] *** PROCESANDO CARTAS ***: {cartasString}");

            try
            {
                string[] partes = cartasString.Split('/');

                // Verificar que tenemos el formato correcto
                if (partes.Length < 5 || !partes[0].Equals("CARDS", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[CARTAS] Formato incorrecto: {cartasString}");
                    return;
                }

                // Crear una nueva lista temporal
                List<Texture2D> cartasTemp = new List<Texture2D>();

                // Procesar cada tipo de carta con más depuración
                int totalCartas = 0;
                for (int i = 0; i < 4; i++) // 4 tipos de cartas: as, jack, king, queen
                {
                    if (int.TryParse(partes[i + 1], out int cantidad))
                    {
                        // Verificar índice dentro de rango
                        if (i < _cartas.Length)
                        {
                            Debug.WriteLine($"[CARTAS] Tipo {i} ({GetNombreCarta(i)}): {cantidad} cartas");

                            // Verificar textura no nula
                            if (_cartas[i] == null)
                            {
                                Debug.WriteLine($"[CARTAS] ¡ERROR! La textura para {GetNombreCarta(i)} es nula");
                                continue;
                            }

                            for (int j = 0; j < cantidad; j++)
                            {
                                cartasTemp.Add(_cartas[i]);
                                totalCartas++;
                                Debug.WriteLine($"[CARTAS] Añadida carta {GetNombreCarta(i)} #{j + 1}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[CARTAS] ¡ERROR! Índice {i} fuera de rango para _cartas[]");
                        }
                    }
                }

                // Validar que tenemos cartas para mostrar y añadir las 4 cartas si no hay
                if (totalCartas == 0)
                {
                    Debug.WriteLine("[CARTAS] ¡ADVERTENCIA! No se añadieron cartas. Usando respaldo.");
                    for (int i = 0; i < 4; i++)
                    {
                        if (_cartas[4] != null)
                        {
                            cartasTemp.Add(_cartas[4]);
                            Debug.WriteLine($"[CARTAS] Añadida carta de respaldo #{i + 1}");
                        }
                        else
                        {
                            Debug.WriteLine("[CARTAS] ¡ERROR! La carta de respaldo es nula");
                        }
                    }
                }

                // Mostrar detalles de las cartas antes de actualizar
                Debug.WriteLine($"[CARTAS] Cartas a añadir: {cartasTemp.Count}");
                for (int i = 0; i < cartasTemp.Count; i++)
                {
                    string nombreCarta = GetNombreCartaPorTextura(cartasTemp[i]);
                    Debug.WriteLine($"[CARTAS] Carta {i}: {nombreCarta}");
                }

                // Actualizar la lista real de forma segura
                _cartasDisponibles = new List<Texture2D>(cartasTemp);

                // Asegurarse de actualizar los filtros
                _cartasConFiltro = new List<bool>(_cartasDisponibles.Count);
                for (int i = 0; i < _cartasDisponibles.Count; i++)
                    _cartasConFiltro.Add(false);

                Debug.WriteLine($"[CARTAS] *** ACTUALIZACIÓN COMPLETA ***. Total cartas: {_cartasDisponibles.Count}");

                // Actualizar posiciones
                ActualizarPosicionesCartas();

                // Forzar redibujado
                _mostrarTodas = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CARTAS] Error crítico: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Añade este método para obtener el nombre de una carta por su índice
        private string GetNombreCarta(int i)
        {
            switch (i)
            {
                case 0: return "ace";
                case 1: return "jack";
                case 2: return "king";
                case 3: return "queen";
                case 4: return "cardback";
                default: return "desconocida";
            }
        }

        // Añade este método para obtener el nombre de una carta por su textura
        private string GetNombreCartaPorTextura(Texture2D textura)
        {
            if (textura == _cartas[0]) return "ace";
            if (textura == _cartas[1]) return "jack";
            if (textura == _cartas[2]) return "king";
            if (textura == _cartas[3]) return "queen";
            if (textura == _cartas[4]) return "cardback";
            return "desconocida";
        }

        // Añade este método para actualizar las posiciones cuando cambia el número de cartas
        private void ActualizarPosicionesCartas()
        {
            _posicionCartasNormal = new Vector2(
                GraphicsDevice.Viewport.Width / 2 - (_cartasDisponibles.Count * 60),
                GraphicsDevice.Viewport.Height - 150);

            _posicionCartasAcercadas = new Vector2(
                GraphicsDevice.Viewport.Width / 2 - (_cartasDisponibles.Count * 90),
                GraphicsDevice.Viewport.Height / 2 - 112);

            Debug.WriteLine($"[CARTAS] Posiciones actualizadas - Normal: {_posicionCartasNormal}, Acercada: {_posicionCartasAcercadas}");
        }

        private void GetCards(string usuario)
        {
            try
            {
                // Verificar si el hilo de escucha está activo
                if (messageListenerThread == null || !messageListenerThread.IsAlive)
                {
                    Debug.WriteLine("[CARTAS] Reiniciando hilo de escucha...");
                    StartMessageListener();
                    Thread.Sleep(100); // Pequeña pausa para dar tiempo a que se inicie el hilo
                }

                // Enviar solicitud
                if (server != null && server.Connected)
                {
                    string mensaje = "9/" + usuario;
                    byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                    server.Send(msg);
                    Debug.WriteLine("[CARTAS] Solicitud enviada para nueva ronda");
                }
                else
                {
                    Debug.WriteLine("[ERROR] No se pueden solicitar cartas - sin conexión");
                    ConnectToServerIfNeeded(); // Intentar reconectar
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ERROR] Error al solicitar cartas: " + ex.Message);
                // Intentar reconectar si es un problema de conexión
                if (ex is SocketException || ex is ObjectDisposedException)
                {
                    ConnectToServerIfNeeded();
                }
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
                        historialChat.Add($"{nombre}: {mensajeChat}");
                    }
                    else
                    {
                        // Mensaje de sistema o de otro formato
                        historialChat.Add(mensaje);
                    }

                    ActualizarSoloPanelMensajes();

                    Debug.WriteLine("[CHAT] Mensaje añadido al historial y panel actualizado");
                    return;
                }

                // **NO RECREAR LA UI SI YA EXISTE Y SOLO AÑADIMOS MENSAJE**
                if (!string.IsNullOrEmpty(mensaje) && UserInterface.Active != null && UserInterface.Active.Root.Children.Count > 0)
                {
                    // Solo añadir el mensaje sin recrear toda la UI
                    Debug.WriteLine("[CHAT] Mensaje añadido al historial sin recrear UI");
                    return;
                }

                // Solo recrear UI si realmente es necesario
                if (UserInterface.Active == null || UserInterface.Active.Root.Children.Count == 0)
                {
                    // Crear la UI completa solo la primera vez o si está vacía
                    CrearInterfazChatCompleta(visible);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en ChatPanel: {ex.Message}");
                CrearInterfazChatCompleta(visible);
            }
        }

        // Reemplazar el método CrearInterfazChatCompleta:

        private void CrearInterfazChatCompleta(bool visible)
        {
            try
            {
                // Limpiar solo si es necesario
                if (UserInterface.Active != null)
                {
                    UserInterface.Active.Clear();
                }

                // Crear el panel principal
                Panel panel = new Panel(
                    new Vector2(350, GraphicsDevice.Viewport.Height),
                    PanelSkin.Simple,
                    Anchor.TopRight);

                panel.Padding = new Vector2(15, 15);
                panel.Visible = visible;
                UserInterface.Active.AddEntity(panel);

                // Encabezado
                Header header = new Header("Chat");
                panel.AddChild(header);
                panel.AddChild(new HorizontalLine());

                // **MOSTRAR INFORMACIÓN DEL JUEGO**
                if (!string.IsNullOrEmpty(carta_ronda_actual))
                {
                    Header cartaRonda = new Header($"Carta Ronda: {carta_ronda_actual}");
                    cartaRonda.FillColor = Color.Yellow;
                    cartaRonda.Scale = 0.8f;
                    panel.AddChild(cartaRonda);
                }

                if (!string.IsNullOrEmpty(jugadorConTurnoActual))
                {
                    Paragraph turnoInfo = new Paragraph(esMiTurno ?
                        "¡ES TU TURNO!" : $"Turno de: {jugadorConTurnoActual}");
                    turnoInfo.FillColor = esMiTurno ? Color.LightGreen : Color.LightBlue;
                    panel.AddChild(turnoInfo);
                }

                panel.AddChild(new HorizontalLine());

                // **PANEL DE MENSAJES - AJUSTAR ALTURA PARA DEJAR ESPACIO A LOS BOTONES**
                int altoPanelMensajes = GraphicsDevice.Viewport.Height - 450; // Más espacio para botones

                Panel mensajesPanel = new Panel(new Vector2(0, altoPanelMensajes), PanelSkin.ListBackground);
                mensajesPanel.Padding = new Vector2(8, 8);

                // **SCROLL AUTOMÁTICO - MOSTRAR ÚLTIMOS MENSAJES**
                int mensajesPorPantalla = Math.Max(10, (altoPanelMensajes - 20) / 25);
                int totalMensajes = historialChat?.Count ?? 0;
                int indiceInicio = Math.Max(0, totalMensajes - mensajesPorPantalla);

                if (historialChat != null && totalMensajes > 0)
                {
                    for (int i = indiceInicio; i < totalMensajes; i++)
                    {
                        if (i >= 0 && i < historialChat.Count)
                        {
                            string msg = historialChat[i];
                            if (!string.IsNullOrEmpty(msg))
                            {
                                Paragraph mensajeParagraph = new Paragraph(msg);
                                mensajeParagraph.Scale = 0.8f;
                                mensajeParagraph.WrapWords = true;

                                // **COLOREAR MENSAJES**
                                if (msg.StartsWith($"{usuario}:"))
                                {
                                    mensajeParagraph.FillColor = Color.LightCyan;
                                }
                                else if (msg.StartsWith("Sistema:"))
                                {
                                    mensajeParagraph.FillColor = Color.Yellow;
                                }
                                else
                                {
                                    mensajeParagraph.FillColor = Color.White;
                                }

                                mensajesPanel.AddChild(mensajeParagraph);
                            }
                        }
                    }
                }
                else
                {
                    Paragraph noMensajes = new Paragraph("No hay mensajes aún...");
                    noMensajes.FillColor = Color.Gray;
                    noMensajes.Scale = 0.8f;
                    mensajesPanel.AddChild(noMensajes);
                }

                panel.AddChild(mensajesPanel);

                // **CAMPO DE TEXTO**
                panel.AddChild(new HorizontalLine());

                TextInput text = new TextInput(false);
                text.PlaceholderText = "Escribe un mensaje...";
                text.Size = new Vector2(0, 40);
                panel.AddChild(text);

                // **BOTONES EN LA PARTE INFERIOR**
                // Botón enviar mensaje
                Button enviarBtn = new Button("Enviar");
                enviarBtn.Size = new Vector2(150, 35);
                enviarBtn.Anchor = Anchor.TopLeft;
                enviarBtn.OnClick = (Entity btn) => EnviarMensajeChat(text);
                panel.AddChild(enviarBtn);

                // **BOTÓN PEDIR CARTAS - SIEMPRE DISPONIBLE PERO CON LIMITACIONES**
                Button cartasBtn = new Button("Pedir Cartas");
                cartasBtn.Size = new Vector2(150, 35);
                cartasBtn.Anchor = Anchor.TopRight;

                // **VERIFICAR SI YA SE PIDIERON CARTAS EN ESTA RONDA**
                bool cartasYaPedidas = VerificarCartasYaPedidas();

                if (cartasYaPedidas)
                {
                    cartasBtn.FillColor = Color.Gray;
                    cartasBtn.Enabled = false;
                }
                else
                {
                    cartasBtn.FillColor = Color.LightBlue;
                    cartasBtn.Enabled = true;
                }

                cartasBtn.OnClick = (Entity btn) => SolicitarNuevasCartasConLimite();
                panel.AddChild(cartasBtn);

                Debug.WriteLine("[CHAT] Interfaz completa creada correctamente");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error creando interfaz completa: {ex.Message}");
            }
        }

        private void SolicitarNuevasCartasConLimite()
        {
            try
            {
                // **VERIFICAR SI YA SE PIDIERON CARTAS EN ESTA RONDA**
                if (cartasPedidasEstaRonda)
                {
                    historialChat.Add("Sistema: Ya has pedido cartas en esta ronda");
                    ActualizarSoloPanelMensajes();
                    return;
                }

                if (conectado || ConnectToServerIfNeeded())
                {
                    GetCards(usuario);
                    cartasPedidasEstaRonda = true; // **MARCAR COMO PEDIDAS**

                    historialChat.Add("Sistema: Solicitando nuevas cartas...");
                    ActualizarSoloPanelMensajes();

                    // **RECREAR UI PARA DESHABILITAR EL BOTÓN**
                    CrearInterfazChatCompleta(true);

                    Debug.WriteLine("[CARTAS] Cartas solicitadas - marcado como usado en esta ronda");
                }
                else
                {
                    historialChat.Add("Sistema: ERROR - No hay conexión al servidor");
                    ActualizarSoloPanelMensajes();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error solicitando cartas: {ex.Message}");
                historialChat.Add($"Sistema: ERROR - {ex.Message}");
                ActualizarSoloPanelMensajes();
            }
        }

        private bool VerificarCartasYaPedidas()
        {
            // Retorna true si ya se pidieron cartas en esta ronda
            return cartasPedidasEstaRonda;
        }

        private void SolicitarNuevasCartas()
        {
            try
            {
                if (conectado || ConnectToServerIfNeeded())
                {
                    GetCards(usuario);
                    historialChat.Add("Sistema: Solicitando nuevas cartas...");
                    // NO recrear UI aquí, solo añadir mensaje
                }
                else
                {
                    historialChat.Add("Sistema: ERROR - No hay conexión al servidor");
                    CrearInterfazChatCompleta(true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error solicitando cartas: {ex.Message}");
                historialChat.Add($"Sistema: ERROR - {ex.Message}");
                CrearInterfazChatCompleta(true);
            }
        }

        private void EnviarMensajeChat(TextInput text)
        {
            if (text == null || string.IsNullOrEmpty(text.Value)) return;

            try
            {
                string mensaje = text.Value.Trim();

                if (server != null && server.Connected)
                {
                    string mensajeFormato = "11/" + usuario + "/" + mensaje;
                    byte[] msg = Encoding.ASCII.GetBytes(mensajeFormato);
                    server.Send(msg);

                    // **AÑADIR INMEDIATAMENTE MI PROPIO MENSAJE AL HISTORIAL**
                    historialChat.Add($"{usuario}: {mensaje}");

                    // **ACTUALIZAR EL CHAT INMEDIATAMENTE**
                    ActualizarSoloPanelMensajes();

                    // **LIMPIAR CAMPO DE TEXTO**
                    text.Value = "";

                    Debug.WriteLine("[CHAT] Mensaje enviado y añadido al historial");
                }
                else
                {
                    historialChat.Add("Sistema: Error de conexión");
                    ActualizarSoloPanelMensajes();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error enviando mensaje: {ex.Message}");
                historialChat.Add($"Sistema: Error - {ex.Message}");
                ActualizarSoloPanelMensajes();
            }
        }

        private void ConnectToServer()
        {
            if (server != null && server.Connected)
                return;

            try
            {
                IPAddress direc = IPAddress.Parse("84.235.233.248");
                IPEndPoint ipep = new IPEndPoint(direc, 50756);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                server.Connect(ipep);
                conectado = true;
                Debug.WriteLine("Conexión al servidor establecida.");
            }
            catch (Exception ex) // Capturar cualquier excepción, no solo SocketException
            {
                Debug.WriteLine("Error al conectar al servidor: " + ex.Message);
                conectado = false;
                server = null; // Asegurarse de que server sea null si hay error
                throw new Exception("No se pudo conectar al servidor.");
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

        private void MensajesPrueba()
        {
            string mensajeFormato = "1000/" + usuario + "/" + "mensaje de prueba";
            byte[] msg = Encoding.ASCII.GetBytes(mensajeFormato);

            for (int i = 0; i < 7; i++)
            {
                server.Send(msg);
            }

            Debug.WriteLine("[PRUEBA] Mensajes de prueba enviados.");
        }

        // Reemplaza el método StartMessageListener con esta versión mejorada
        private void StartMessageListener()
        {
            stopMessageListener = false;
            Debug.WriteLine("Iniciando hilo de escucha de mensajes...");

            messageListenerThread = new Thread(() =>
            {
                try
                {
                    Debug.WriteLine("[RED] Hilo de escucha iniciado correctamente");
                    while (!stopMessageListener)
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
                                        continue;
                                    }
                                    Debug.WriteLine($"[RED] Error de socket: {se.Message}");
                                    Thread.Sleep(500);
                                    continue;
                                }

                                if (bytesReceived > 0)
                                {
                                    string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                                    Debug.WriteLine($"[LISTENER] *** MENSAJE RAW RECIBIDO: '{message}' ***");

                                    // **DEBUG ESPECÍFICO PARA ELIMINACIÓN Y FIN DE PARTIDA**
                                    if (message.Contains("JUGADOR_ELIMINADO") || message.Contains("FIN_PARTIDA"))
                                    {
                                        Debug.WriteLine($"[LISTENER] *** MENSAJE CRÍTICO DETECTADO: {message} ***");
                                    }
                                    else if (message.Contains("CHAT/"))
                                    {
                                        Debug.WriteLine($"[LISTENER] *** MENSAJE DE CHAT DETECTADO: {message} ***");
                                    }

                                    ProcessServerMessage(message);
                                }
                            }
                            else
                            {
                                Debug.WriteLine("[RED] Socket no conectado, esperando...");
                                Thread.Sleep(1000);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[RED] Error en hilo de escucha: {ex.Message}");
                            Thread.Sleep(1000);
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    Debug.WriteLine("[RED] Hilo de escucha abortado");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RED] Error fatal en hilo de escucha: {ex.Message}");
                }
                finally
                {
                    Debug.WriteLine("[RED] Hilo de escucha finalizado.");
                }
            });

            messageListenerThread.IsBackground = true;
            messageListenerThread.Start();
        }

        // Método para separar mensajes concatenados
        private string[] SepararMensajesConcatenados(string mensajeCrudo)
        {
            List<string> mensajes = new List<string>();

            // **MEJORAR PATRONES Y USAR SALTOS DE LÍNEA**
            string[] patrones = {
        "CARDS/", "TURN/", "ACTION/", "CHAT/", "DESAFIO/", "JUGADOR_ELIMINADO/",
        "FIN_PARTIDA/", "GRUPO_DISUELTO/", "PARTIDA_CANCELADA/", "NUEVA_RONDA/",
        "ERROR/", "ACTION_OK/", "CARTA_RONDA", "PARTIDA/", "MSG/"
    };

            // **PRIMER INTENTO: SEPARAR POR SALTOS DE LÍNEA**
            string[] lineas = mensajeCrudo.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (lineas.Length > 1)
            {
                // Si hay múltiples líneas, cada una es un mensaje
                foreach (string linea in lineas)
                {
                    string lineaLimpia = linea.Trim();
                    if (!string.IsNullOrEmpty(lineaLimpia))
                    {
                        mensajes.Add(lineaLimpia);
                    }
                }
                return mensajes.ToArray();
            }

            // **SEGUNDO INTENTO: BUSCAR PATRONES**
            int inicio = 0;
            for (int i = 1; i < mensajeCrudo.Length; i++)
            {
                foreach (string patron in patrones)
                {
                    if (i + patron.Length <= mensajeCrudo.Length &&
                        mensajeCrudo.Substring(i, patron.Length) == patron)
                    {
                        string mensajeAnterior = mensajeCrudo.Substring(inicio, i - inicio).Trim();
                        if (!string.IsNullOrEmpty(mensajeAnterior))
                        {
                            mensajes.Add(mensajeAnterior);
                        }
                        inicio = i;
                        break;
                    }
                }
            }

            // Añadir el último mensaje
            string ultimoMensaje = mensajeCrudo.Substring(inicio).Trim();
            if (!string.IsNullOrEmpty(ultimoMensaje))
            {
                mensajes.Add(ultimoMensaje);
            }

            // Si no se encontraron patrones, devolver el mensaje original
            if (mensajes.Count == 0)
            {
                mensajes.Add(mensajeCrudo.Trim());
            }

            return mensajes.ToArray();
        }

        private void EnviarCartasSeleccionadas()
        {
            if (!esMiTurno)
            {
                ChatPanel(true, "Sistema/No puedes jugar cartas fuera de tu turno");
                return;
            }

            try
            {
                // Contar cuántas cartas tienen filtro aplicado
                int cartasConFiltroCount = 0;
                List<string> tiposCartas = new List<string>();

                // Verificar que la lista de filtros esté inicializada
                if (_cartasConFiltro == null || _cartasConFiltro.Count < _cartasDisponibles.Count)
                {
                    // Inicializar la lista de filtros si es necesario
                    _cartasConFiltro = new List<bool>();
                    while (_cartasConFiltro.Count < _cartasDisponibles.Count)
                    {
                        _cartasConFiltro.Add(false);
                    }
                }

                // Recorrer todas las cartas y verificar cuáles tienen filtro
                for (int i = 0; i < _cartasDisponibles.Count; i++)
                {
                    if (_cartasConFiltro[i])
                    {
                        cartasConFiltroCount++;
                        // Obtener el tipo de cada carta (del 1 al 4)
                        string tipoCarta = GetTipoCartaPorTextura(_cartasDisponibles[i]);
                        tiposCartas.Add(tipoCarta);
                    }
                }

                // Verificar si hay cartas seleccionadas
                if (cartasConFiltroCount == 0)
                {
                    ChatPanel(true, "Sistema/No has seleccionado ninguna carta para enviar");
                    return;
                }

                // Construir el mensaje con la cantidad de cartas y sus tipos
                string mensaje = $"21/{usuario}/JUGAR/{cartasConFiltroCount}";

                // Añadir cada carta al mensaje
                foreach (string tipoCarta in tiposCartas)
                {
                    mensaje += $"/{tipoCarta}";
                }

                Debug.WriteLine($"[CARTAS] *** MENSAJE CONSTRUIDO: '{mensaje}' ***");

                // Enviar al servidor
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine($"[ACCIÓN] Enviadas {cartasConFiltroCount} cartas: {string.Join(", ", tiposCartas)}");
                ChatPanel(true, $"Sistema/Has enviado {cartasConFiltroCount} carta(s): {string.Join(", ", tiposCartas)}");

                // Limpiar las cartas enviadas de tu mano
                LimpiarCartasEnviadas();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al enviar cartas: {ex.Message}");
                ChatPanel(true, $"Sistema/ERROR: {ex.Message}");
            }
        }

        /*private void EnviarConfirmacionEliminacion()
        {
            try
            {
                // Enviar mensaje código 25 para confirmar eliminación
                string mensaje = $"25/{usuario}/Lala";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine($"[DESAFÍO] Confirmación de eliminación enviada");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al enviar confirmación de eliminación: {ex.Message}");
            }
        }*/

        private void LimpiarCartasEnviadas()
        {
            if (_cartasConFiltro == null || _cartasDisponibles == null)
                return;

            Debug.WriteLine($"[CARTAS] Antes de limpiar: {_cartasDisponibles.Count} cartas disponibles");

            // Crear listas temporales para las cartas que NO tienen filtro
            List<Texture2D> cartasRestantes = new List<Texture2D>();
            List<bool> filtrosRestantes = new List<bool>();

            // Recorrer todas las cartas y conservar solo las que NO tienen filtro
            for (int i = 0; i < Math.Min(_cartasDisponibles.Count, _cartasConFiltro.Count); i++)
            {
                if (!_cartasConFiltro[i]) // Si NO tiene filtro (no fue enviada)
                {
                    cartasRestantes.Add(_cartasDisponibles[i]);
                    filtrosRestantes.Add(false); // Resetear filtro
                    Debug.WriteLine($"[CARTAS] Carta {i} conservada: {GetNombreCartaPorTextura(_cartasDisponibles[i])}");
                }
                else
                {
                    Debug.WriteLine($"[CARTAS] Carta {i} eliminada: {GetNombreCartaPorTextura(_cartasDisponibles[i])}");
                }
            }

            // Actualizar las listas principales
            _cartasDisponibles = cartasRestantes;
            _cartasConFiltro = filtrosRestantes;

            // Resetear el índice de carta seleccionada
            if (_cartasDisponibles.Count > 0)
            {
                _cartaSeleccionadaIndex = 0;
            }
            else
            {
                _cartaSeleccionadaIndex = -1;
            }

            // Actualizar posiciones
            ActualizarPosicionesCartas();

            Debug.WriteLine($"[CARTAS] Después de limpiar: {_cartasDisponibles.Count} cartas restantes");
        }

        public override void Update(GameTime gameTime)
        {
            try
            {
                // Procesar mensajes pendientes primero
                List<string> mensajesAhora = null;
                bool mensajesProcesados = false;

                lock (mensajesLock)
                {
                    if (hayNuevosMensajes && mensajesPendientes.Count > 0)
                    {
                        mensajesAhora = new List<string>(mensajesPendientes);
                        mensajesPendientes.Clear();
                        hayNuevosMensajes = false;
                        mensajesProcesados = true;
                        Debug.WriteLine($"[UPDATE] Procesando {mensajesAhora.Count} mensajes pendientes");
                    }
                    else if (hayNuevosMensajes)
                    {
                        hayNuevosMensajes = false;
                    }
                }

                if (mensajesAhora != null)
                {
                    foreach (string mensaje in mensajesAhora)
                    {
                        Debug.WriteLine($"[UPDATE] *** PROCESANDO MENSAJE PENDIENTE: {mensaje} ***");

                        if (mensaje == "MOSTRAR_PANEL_ELIMINADO_GEONBIT")
                        {
                            Debug.WriteLine("[UPDATE] *** MOSTRANDO PANEL GEONBIT DE ELIMINACIÓN ***");
                            MostrarPanelEliminacionGeonbit();
                            return; // **SALIR INMEDIATAMENTE**
                        }
                        else if (mensaje.StartsWith("MOSTRAR_PANEL_FIN_PARTIDA/"))
                        {
                            string ganador = mensaje.Substring(26);
                            Debug.WriteLine($"[UPDATE] *** MOSTRANDO PANEL FIN PARTIDA - GANADOR: {ganador} ***");
                            MostrarPanelFinPartida(ganador);
                            return; // **SALIR INMEDIATAMENTE**
                        }
                        else if (mensaje.StartsWith("CARDS/"))
                        {
                            Debug.WriteLine("[UPDATE] Procesando cartas en hilo principal");
                            ProcesarCartasDelServidor(mensaje);
                            _mostrarTodas = true;
                        }
                        else if (mensaje == "ACTUALIZAR_CHAT")
                        {
                            Debug.WriteLine("[UPDATE] *** ACTUALIZANDO CHAT ***");
                            ActualizarSoloPanelMensajes();
                        }
                        else
                        {
                            Debug.WriteLine($"[UPDATE] Mensaje no reconocido: {mensaje}");
                        }
                    }
                }
                // **ACTUALIZAR UI SI HAY CAMBIOS EN CHAT**
                if (mensajesProcesados)
                {
                    ActualizarSoloPanelMensajes();
                }

                if (UserInterface.Active != null)
                {
                    UserInterface.Active.Update(gameTime);
                }

                if (estoyEliminado || partidaTerminada)
                {
                    Debug.WriteLine("[UPDATE] Jugador eliminado o partida terminada - evitando lógica de juego");
                    return;
                }

                // Obtener estados de entrada actuales - USAR NOMBRES ÚNICOS
                KeyboardState estadoTeclado = Keyboard.GetState();
                var keyboardExtended = KeyboardExtended.GetState(); // ← CAMBIAR NOMBRE
                var mouseState = MouseExtended.GetState();

                // Permitir que UserInterface procese la entrad

                // Verifica si el ratón está sobre un control de UI antes de procesar teclas de juego
                bool ratónSobreUI = false;
                if (UserInterface.Active != null)
                {
                    // Comprueba si el ratón está sobre alguna entidad de UI
                    // GeonBit.UI no tiene IsMouseOverAnyEntity, pero puedes comprobar si el mouse está sobre alguna entidad así:
                    ratónSobreUI = UserInterface.Active.TargetEntity != null && UserInterface.Active.TargetEntity.IsMouseOver;
                }

                if (mostrandoDesafio)
                {
                    // **INCREMENTAR EL CONTADOR DE TIEMPO**
                    tiempoDesafio += (float)gameTime.ElapsedGameTime.TotalSeconds;

                    // **VERIFICAR SI HA PASADO EL TIEMPO (3 SEGUNDOS)**
                    if (tiempoDesafio >= DURACION_DESAFIO)
                    {
                        // **FINALIZAR LA VISUALIZACIÓN**
                        mostrandoDesafio = false;

                        Debug.WriteLine("[DESAFÍO] Tiempo de visualización completado (3 segundos)");

                        // **LIMPIAR LAS LISTAS DE DESAFÍO**
                        cartasDesafio.Clear();
                        cartasDesafioValidas.Clear();
                    }
                }

                // Solo procesar teclas de juego si el ratón no está sobre la UI
                if (!ratónSobreUI)
                {
                    // **TECLA ESC - SIEMPRE DISPONIBLE**
                    if (keyboardExtended.WasKeyReleased(Keys.Escape))
                    {
                        Debug.WriteLine("[INPUT] Tecla ESC presionada - Mostrando menú de pausa");
                        EscMenu(usuario, true);
                    }

                    // **TECLA Q - SIEMPRE DISPONIBLE**
                    if (keyboardExtended.WasKeyReleased(Keys.Q))
                    {
                        _cartasAcercadas = !_cartasAcercadas;

                        // Si se alejan las cartas, resetear la selección y el filtro
                        if (!_cartasAcercadas)
                        {
                            _cartaSeleccionadaIndex = -1;
                            _cartasConFiltro = new List<bool>();  // Resetear los filtros
                        }

                        Debug.WriteLine($"[INPUT] Cartas acercadas: {_cartasAcercadas}");
                    }

                    // **NAVEGACIÓN CON FLECHAS - SIEMPRE DISPONIBLE CUANDO ESTÁN ACERCADAS**
                    if (_cartasAcercadas)
                    {
                        if (keyboardExtended.WasKeyReleased(Keys.Left))
                        {
                            if (_cartasDisponibles.Count > 0)
                            {
                                _cartaSeleccionadaIndex--;
                                if (_cartaSeleccionadaIndex < 0)
                                    _cartaSeleccionadaIndex = _cartasDisponibles.Count - 1;

                                Debug.WriteLine($"[NAVEGACIÓN] Carta seleccionada: {_cartaSeleccionadaIndex}");
                            }
                        }
                        else if (keyboardExtended.WasKeyReleased(Keys.Right))
                        {
                            if (_cartasDisponibles.Count > 0)
                            {
                                _cartaSeleccionadaIndex = (_cartaSeleccionadaIndex + 1) % _cartasDisponibles.Count;
                                Debug.WriteLine($"[NAVEGACIÓN] Carta seleccionada: {_cartaSeleccionadaIndex}");
                            }
                        }

                        // **TECLA ESPACIO - SIEMPRE DISPONIBLE PARA SELECCIONAR CARTAS**
                        if (keyboardExtended.WasKeyReleased(Keys.Space))
                        {
                            // Solo aplicar filtro si hay una carta seleccionada
                            if (_cartaSeleccionadaIndex >= 0 && _cartaSeleccionadaIndex < _cartasDisponibles.Count)
                            {
                                // Asegurarse de que la lista de filtros tiene suficientes elementos
                                while (_cartasConFiltro.Count < _cartasDisponibles.Count)
                                    _cartasConFiltro.Add(false);

                                // Alternar el filtro solo para la carta seleccionada
                                _cartasConFiltro[_cartaSeleccionadaIndex] = !_cartasConFiltro[_cartaSeleccionadaIndex];

                                Debug.WriteLine(_cartasConFiltro[_cartaSeleccionadaIndex] ?
                                    $"[FILTRO] Aplicado en carta {_cartaSeleccionadaIndex}" :
                                    $"[FILTRO] Removido en carta {_cartaSeleccionadaIndex}");
                            }
                        }

                        // **TECLA F8 - DEBUG - SIEMPRE DISPONIBLE**
                        if (keyboardExtended.WasKeyReleased(Keys.F8))
                        {
                            _permitirAccionesJuego = true;
                            esMiTurno = true;
                            Debug.WriteLine("[FORZADO] Estado de juego forzado a: permitirAcciones=True, esMiTurno=True");
                            ChatPanel(true, "Sistema/FORZADO: Controles habilitados manualmente");
                        }

                        // **ACCIONES QUE REQUIEREN TURNO**
                        if (_permitirAccionesJuego && esMiTurno)
                        {
                            // Tecla E para enviar cartas seleccionadas
                            if (keyboardExtended.WasKeyReleased(Keys.E))
                            {
                                EnviarCartasSeleccionadas();
                            }

                            // Tecla F para desafiar
                            if (keyboardExtended.WasKeyReleased(Keys.F))
                            {
                                DesafiarJugador();
                            }
                        }
                        else if (keyboardExtended.WasKeyReleased(Keys.E) || keyboardExtended.WasKeyReleased(Keys.F))
                        {
                            // **MENSAJE ESPECÍFICO PARA ACCIONES QUE REQUIEREN TURNO**
                            Debug.WriteLine("[INPUT] Intento de acción que requiere turno");
                            ChatPanel(true, "Sistema/No puedes enviar cartas o desafiar fuera de tu turno");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en Update: {ex.Message}");
            }
        }

        // Agregar este nuevo método a GameCardScreen.cs:

        private void MostrarPanelEliminacionDefinitivo()
        {
            try
            {
                Debug.WriteLine("[PANEL] *** CREANDO PANEL DE ELIMINACIÓN DEFINITIVO ***");

                _permitirAccionesJuego = false;
                esMiTurno = false;
                _cartasAcercadas = false;
                _cartaSeleccionadaIndex = -1;

                // **LIMPIAR TODO PRIMERO**
                if (UserInterface.Active != null)
                {
                    UserInterface.Active.Clear();
                    Debug.WriteLine("[PANEL] UI limpiada completamente");
                }

                // **CREAR PANEL PRINCIPAL GRANDE Y VISIBLE**
                Panel panelEliminacion = new Panel(new Vector2(600, 400), PanelSkin.Default, Anchor.Center);
                panelEliminacion.FillColor = Color.Black * 0.9f;
                panelEliminacion.OutlineColor = Color.Red;
                panelEliminacion.OutlineWidth = 5;
                panelEliminacion.Visible = true;

                // **TÍTULO GRANDE Y ROJO**
                Header titulo = new Header("¡HAS SIDO ELIMINADO!");
                titulo.FillColor = Color.Red;
                titulo.Scale = 1.8f;
                panelEliminacion.AddChild(titulo);

                panelEliminacion.AddChild(new HorizontalLine());

                // **MENSAJE EXPLICATIVO**
                panelEliminacion.AddChild(new Paragraph("Has sido eliminado de la partida."));
                panelEliminacion.AddChild(new Paragraph("La partida continúa sin ti."));
                panelEliminacion.AddChild(new HorizontalLine());

                // **BOTÓN PARA REGRESAR**
                Button regresarBtn = new Button("Regresar al Menú", ButtonSkin.Default);
                regresarBtn.Size = new Vector2(250, 60);
                regresarBtn.FillColor = Color.Orange;
                regresarBtn.Scale = 1.2f;
                regresarBtn.OnClick = (Entity btn) =>
                {
                    Debug.WriteLine("[PANEL] Botón regresar clickeado - eliminado");
                    RegresarAlMenuPrincipal();
                };
                panelEliminacion.AddChild(regresarBtn);

                // **AÑADIR A LA UI**
                UserInterface.Active.AddEntity(panelEliminacion);

                Debug.WriteLine("[PANEL] *** PANEL DE ELIMINACIÓN CREADO Y MOSTRADO ***");

                // **AUTO-REGRESAR DESPUÉS DE 8 SEGUNDOS**
                // En MostrarPanelEliminacionDefinitivo, cambiar el auto-regreso:

                // **AUTO-REGRESAR DESPUÉS DE 15 SEGUNDOS EN LUGAR DE 8**
                System.Threading.Tasks.Task.Delay(15000).ContinueWith(_ =>
                {
                    try
                    {
                        if (panelEliminacion != null && panelEliminacion.Visible && estoyEliminado)
                        {
                            Debug.WriteLine("[PANEL] Auto-regreso después de 15 segundos");
                            RegresarAlMenuPrincipal();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PANEL] Error en auto-regreso: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] *** Error crítico creando panel eliminación: {ex.Message} ***");

                // **PLAN DE RESPALDO**
                RegresarAlMenuPrincipal();
            }
        }

        private void DesafiarJugador()
        {
            try
            {
                // Enviar acción de desafío al servidor
                string mensaje = $"24/{usuario}";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine("[ACCIÓN] Desafío enviado al servidor");
                ChatPanel(true, "Sistema/Desafío enviado al servidor");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al enviar desafío: {ex.Message}");
                ChatPanel(true, $"Sistema/ERROR: {ex.Message}");
            }
        }

        public override void Draw(GameTime gameTime)
        {
            try
            {
                GraphicsDevice.Clear(Color.CornflowerBlue);

                if (_spriteBatch == null)
                {
                    _spriteBatch = new SpriteBatch(GraphicsDevice);
                }

                _spriteBatch.Begin();

                _spriteBatch.Draw(_background, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);

                // Solo mostrar cartas si existen y las texturas son válidas
                if (_mostrarTodas && _cartasDisponibles != null && _cartasDisponibles.Count > 0)
                {
                    float escalaActual = _cartasAcercadas ? _escalaCartasAcercadas : _escalaCartasNormal;

                    // Verificar que todas las texturas sean válidas antes de dibujar
                    List<Texture2D> cartasValidas = new List<Texture2D>();
                    for (int i = 0; i < _cartasDisponibles.Count; i++)
                    {
                        if (_cartasDisponibles[i] != null && !_cartasDisponibles[i].IsDisposed)
                        {
                            cartasValidas.Add(_cartasDisponibles[i]);
                        }
                    }

                    if (cartasValidas.Count > 0)
                    {
                        // Recalcular posición base para centrar
                        float anchoTotal = cartasValidas.Count * (150 * escalaActual * 0.8f);
                        float posX = GraphicsDevice.Viewport.Width / 2 - anchoTotal / 2;

                        for (int i = 0; i < cartasValidas.Count; i++)
                        {
                            try
                            {
                                int x = (int)(posX + (i * 150 * escalaActual * 0.8f));
                                int y = _cartasAcercadas ? GraphicsDevice.Viewport.Height / 2 - 112
                                                         : GraphicsDevice.Viewport.Height - 150;

                                int ancho = (int)(150 * escalaActual);
                                int alto = (int)(225 * escalaActual);

                                // Verificar límites del rectángulo
                                if (x >= 0 && y >= 0 && ancho > 0 && alto > 0 &&
                                    x + ancho <= GraphicsDevice.Viewport.Width &&
                                    y + alto <= GraphicsDevice.Viewport.Height)
                                {
                                    Color colorTextura = Color.White;

                                    // Aplicar filtro si corresponde
                                    if (_cartasConFiltro.Count > i && _cartasConFiltro[i])
                                    {
                                        colorTextura = _colorFiltro;
                                    }

                                    // Dibujar borde si es la carta seleccionada
                                    if (i == _cartaSeleccionadaIndex && _cartasAcercadas)
                                    {
                                        DibujarBordeCarta(x, y, ancho, alto, Color.Yellow);
                                    }

                                    // Dibujar la carta
                                    _spriteBatch.Draw(
                                        cartasValidas[i],
                                        new Rectangle(x, y, ancho, alto),
                                        colorTextura
                                    );
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[DRAW] Error dibujando carta {i}: {ex.Message}");
                            }
                        }
                    }
                }

                // Dibujar sistema de desafío si está activo
                if (mostrandoDesafio && cartasDesafio.Count > 0)
                {
                    DibujarPanelDesafio();
                }

                _spriteBatch.End();

                // Dibujar la interfaz de usuario
                try
                {
                    if (UserInterface.Active != null)
                    {
                        UserInterface.Active.Draw(_spriteBatch);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DRAW] Error dibujando UI: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DRAW] Error general: {ex.Message}");
            }
        }

        // Reemplazar MostrarPanelEliminacionGeonbit completo:

        private void MostrarPanelEliminacionGeonbit()
        {
            try
            {
                Debug.WriteLine("[UI] *** INICIANDO MostrarPanelEliminacionGeonbit ***");

                // **FORZAR LIMPIEZA COMPLETA**
                _permitirAccionesJuego = false;
                esMiTurno = false;
                _cartasAcercadas = false;
                _cartaSeleccionadaIndex = -1;

                if (UserInterface.Active != null)
                {
                    UserInterface.Active.Clear();
                    Debug.WriteLine("[UI] UI limpiada completamente");
                }

                // **CREAR PANEL MÁS GRANDE Y VISIBLE**
                Panel panel = new Panel(new Vector2(600, 400), PanelSkin.Default, Anchor.Center);
                panel.FillColor = Color.Black * 0.9f;
                panel.OutlineColor = Color.Red;
                panel.OutlineWidth = 6;
                panel.Visible = true;

                // **TÍTULO MUY VISIBLE**
                Header titulo = new Header("¡HAS SIDO ELIMINADO!");
                titulo.FillColor = Color.Red;
                titulo.Scale = 2.0f; // **MÁS GRANDE**
                panel.AddChild(titulo);

                panel.AddChild(new HorizontalLine());

                // **MENSAJES CLAROS**
                Paragraph mensaje1 = new Paragraph("Has sido eliminado de la partida.");
                mensaje1.FillColor = Color.White;
                mensaje1.Scale = 1.3f;
                panel.AddChild(mensaje1);

                Paragraph mensaje2 = new Paragraph("La partida ha terminado para ti.");
                mensaje2.FillColor = Color.Orange;
                mensaje2.Scale = 1.2f;
                panel.AddChild(mensaje2);

                panel.AddChild(new HorizontalLine());

                // **BOTÓN MUY VISIBLE**
                Button regresarBtn = new Button("REGRESAR AL MENÚ");
                regresarBtn.Size = new Vector2(300, 80);
                regresarBtn.FillColor = Color.Orange;
                regresarBtn.Scale = 1.3f;
                regresarBtn.OnClick = (Entity btn) =>
                {
                    Debug.WriteLine("[ELIMINACIÓN] *** BOTÓN REGRESAR CLICKEADO ***");
                    RegresarAlMenuPrincipal();
                };
                panel.AddChild(regresarBtn);

                // **AÑADIR AL UI**
                UserInterface.Active.AddEntity(panel);

                Debug.WriteLine("[UI] *** PANEL ELIMINACIÓN GEONBIT CREADO Y VISIBLE ***");

                // **AUTO-REGRESAR MÁS RÁPIDO**
                // En MostrarPanelEliminacionGeonbit, cambiar el auto-regreso:

                // **AUTO-REGRESAR DESPUÉS DE 15 SEGUNDOS EN LUGAR DE 7**
                System.Threading.Tasks.Task.Delay(15000).ContinueWith(_ =>
                {
                    if (panel != null && panel.Visible && estoyEliminado)
                    {
                        Debug.WriteLine("[UI] Auto-regreso por timeout después de 15 segundos");
                        RegresarAlMenuPrincipal();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] *** ERROR CRÍTICO EN PANEL ELIMINACIÓN: {ex.Message} ***");
                RegresarAlMenuPrincipal();
            }
        }

        private void DibujarBordeCarta(int x, int y, int ancho, int alto, Color color)
        {
            try
            {
                Texture2D bordeTextura = GetOrCreatePlainTexture(color);
                int bordeGrosor = 3;

                // Dibujar bordes con verificación de límites
                if (bordeTextura != null)
                {
                    _spriteBatch.Draw(bordeTextura, new Rectangle(x - bordeGrosor, y - bordeGrosor, ancho + bordeGrosor * 2, bordeGrosor), color);
                    _spriteBatch.Draw(bordeTextura, new Rectangle(x - bordeGrosor, y + alto, ancho + bordeGrosor * 2, bordeGrosor), color);
                    _spriteBatch.Draw(bordeTextura, new Rectangle(x - bordeGrosor, y - bordeGrosor, bordeGrosor, alto + bordeGrosor * 2), color);
                    _spriteBatch.Draw(bordeTextura, new Rectangle(x + ancho, y - bordeGrosor, bordeGrosor, alto + bordeGrosor * 2), color);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DRAW] Error dibujando borde: {ex.Message}");
            }
        }

        private void DibujarPanelDesafio()
        {
            try
            {
                // Dibujar fondo semitransparente
                Texture2D fondoTextura = GetOrCreatePlainTexture(new Color(0, 0, 0, 180));
                _spriteBatch.Draw(fondoTextura, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);

                // Calcular posición central para las cartas
                int anchoTotal = cartasDesafio.Count * 160;
                int inicioX = GraphicsDevice.Viewport.Width / 2 - anchoTotal / 2;
                int posY = GraphicsDevice.Viewport.Height / 2 - 150;

                // Dibujar cada carta con su filtro
                for (int i = 0; i < cartasDesafio.Count; i++)
                {
                    if (cartasDesafio[i] != null && !cartasDesafio[i].IsDisposed)
                    {
                        int posX = inicioX + (i * 160);

                        // Verificar límites
                        if (posX >= 0 && posY >= 0 && posX + 150 <= GraphicsDevice.Viewport.Width)
                        {
                            Color colorFiltro = cartasDesafioValidas[i] ?
                                new Color(0, 255, 0, 100) :  // Verde
                                new Color(255, 0, 0, 100);   // Rojo

                            // Dibujar la carta
                            _spriteBatch.Draw(cartasDesafio[i], new Rectangle(posX, posY, 150, 225), Color.White);

                            // Dibujar el filtro
                            Texture2D filtroTextura = GetOrCreatePlainTexture(colorFiltro);
                            _spriteBatch.Draw(filtroTextura, new Rectangle(posX, posY, 150, 225), colorFiltro);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DRAW] Error dibujando panel desafío: {ex.Message}");
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