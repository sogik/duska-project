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

namespace Duska.Screens
{
    public class GameCardScreen : GameScreen
    {
        private Texture2D[] _cartas = new Texture2D[5];
        private Texture2D _cartaSeleccionada;
        private bool _mostrarTodas = true;
        private Random _random = new Random();
        private SpriteBatch _spriteBatch;
        private MonoGame.Extended.Input.KeyboardStateExtended _estadoTecladoAnterior;
        private List<Texture2D> _cartasDisponibles = new List<Texture2D>();
        private BuiltinThemes _currTheme;
        private Socket server;
        private Thread messageListenerThread;
        private bool isReconnecting = false;
        private volatile bool stopMessageListener = false;
        private bool conectado = false;

        public string usuario;

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
        private bool _permitirAccionesJuego = false;

        // Variables para el sistema de desafío
        private bool mostrandoDesafio = false;
        private float tiempoDesafio = 0f;
        private const float DURACION_DESAFIO = 5.0f; // 5 segundos de duración
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
                    if (server != null && server.Connected)
                    {
                        // Enviar mensaje para abandonar partida
                        string mensaje = "27/" + usuario;
                        byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                        server.Send(msg);

                        ChatPanel(true, "Sistema/Abandonando partida...");
                        Debug.WriteLine("[ABANDONO] Solicitud enviada para abandonar partida");
                    }

                    UserInterface.Active.Clear();
                    ScreenManager.LoadScreen(new MainMenuScreen(Game, usuario), new FadeTransition(GraphicsDevice, Color.Black, 0.5f));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Error al abandonar partida: {ex.Message}");
                    ChatPanel(true, $"Sistema/Error al abandonar partida: {ex.Message}");
                }

                // Ocultar el panel de confirmación
                panel.Visible = false;
                UserInterface.Active.RemoveEntity(panel);
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
            // Compara directamente con las texturas cargadas en _cartas[]
            if (textura == _cartas[0]) // ace
                return "ace";
            else if (textura == _cartas[1]) // jack
                return "jack";
            else if (textura == _cartas[2]) // king
                return "king";
            else if (textura == _cartas[3]) // queen
                return "queen";
            else if (textura == _cartas[4]) // cardback
                return "0";
            else
                return "0"; // Desconocido
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
                else if (message.StartsWith("CHAT/"))
                {
                    // Mensaje de chat
                    string chatMessage = message.Substring(5).Trim(); ;
                    Debug.WriteLine("Mensaje de chat recibido: " + chatMessage);
                    PedirTurno();

                    // Usar Invoke para actualizar UI desde cualquier hilo
                    if (System.Threading.Thread.CurrentThread.IsBackground)
                    {
                        // Llamada segura a método de UI
                        // En MonoGame normalmente necesitarías una forma de ejecutar esto en el hilo principal
                        lock (mensajesLock)
                        {
                            mensajesPendientes.Add(message);
                            hayNuevosMensajes = true;
                        }
                    }
                    else
                    {
                        ChatPanel(true, chatMessage);
                    }
                    return;
                }
                else if (message.StartsWith("TURN/"))
                {
                    // TURN/jugadorNombre - Indica de quién es el turno
                    string jugadorTurno = message.Substring(5).Trim();

                    esMiTurno = string.Equals(jugadorTurno, usuario, StringComparison.OrdinalIgnoreCase);

                    _permitirAccionesJuego = esMiTurno;

                    jugadorConTurnoActual = jugadorTurno;

                    // Mensaje en el chat
                    if (esMiTurno)
                        ChatPanel(true, "Sistema/¡ES TU TURNO! Selecciona cartas con [ESPACIO] y envíalas con [E]");
                    else
                        ChatPanel(true, $"Sistema/Turno de {jugadorConTurnoActual}. Esperando...");

                    Debug.WriteLine($"[TURNOS] Cambio de turno: {jugadorConTurnoActual} | ¿Es mi turno? {esMiTurno}");

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
                    string mensaje = message.Substring(8).Trim();

                    if (message.StartsWith("EXITO"))
                    {

                        string[] mensaje2 = mensaje.Split('/');
                        string desafiado = mensaje2[1].Trim();

                        string[] cartas = new string[mensaje2.Length - 2];
                        for (int i = 2; i < mensaje2.Length; i++)
                        {
                            cartas[i - 2] = mensaje2[i].Trim();
                        }

                        for (int i = 0; i < cartas.Length; i++)
                        {
                            cartas_jugadas[i] = cartas[i];
                        }

                        PrepararCartasDesafio(cartas_jugadas);

                        ChatPanel(true, $"Sistema/El jugador {usuario} ha desafiado a {desafiado} y como el desafío fue exitoso, {desafiado} ha sido eliminado.");
                    }
                    else if (message.StartsWith("FALLIDO"))
                    {
                        string[] mensaje2 = mensaje.Split('/');
                        string desafiado = mensaje2[1].Trim();
                        ChatPanel(true, $"Sistema/El jugador {usuario} ha desafiado a {desafiado} y como el desafío fue fallido, {usuario} ha sido eliminado.");
                        string[] cartas = new string[mensaje2.Length - 2];
                        for (int i = 2; i < mensaje2.Length; i++)
                        {
                            cartas[i - 2] = mensaje2[i].Trim();
                        }

                        for (int i = 0; i < cartas.Length; i++)
                        {
                            cartas_jugadas[i] = cartas[i];
                        }

                        PrepararCartasDesafio(cartas_jugadas);
                    }
                    return;
                }
                else if (message.StartsWith("CARTA_RONDA"))
                {
                    string carta = message.Substring(12).Trim();
                    carta_ronda_actual = carta;
                    Debug.WriteLine($"[JUEGO] Carta de la ronda actual: {carta_ronda_actual}");
                }
                else if (message.StartsWith("NUEVA_RONDA/"))
                {
                    // Formato: NUEVA_RONDA/numero_ronda
                    string[] partes = message.Split('/');

                    if (partes.Length >= 2)
                    {
                        // Obtener número de ronda y carta
                        string numeroRonda = partes[1];

                        obtener_carta_ronda();

                        // Notificar al usuario
                        ChatPanel(true, $"Sistema/¡Comienza la ronda {numeroRonda}! Carta designada: {carta_ronda_actual}");

                        // Solicitar nuevas cartas para esta ronda
                        if (conectado || ConnectToServerIfNeeded())
                        {
                            // Solicitar cartas nuevas para la nueva ronda
                            GetCards(usuario);
                            PedirTurno();

                            Debug.WriteLine($"[RONDA] Nueva ronda {numeroRonda}, carta: {carta_ronda_actual}. Solicitando nuevas cartas.");
                        }
                    }
                    return;
                }
                else if (message.StartsWith("JUGADOR_ELIMINADO/"))
                {
                    // Formato: JUGADOR_ELIMINADO/nombre_usuario
                    string jugadorEliminado = message.Substring(17).Trim();

                    // Mostrar mensaje de eliminación
                    ChatPanel(true, $"Sistema/¡El jugador {jugadorEliminado} ha sido eliminado!");

                    // Si el jugador eliminado soy yo
                    if (jugadorEliminado.Equals(usuario, StringComparison.OrdinalIgnoreCase))
                    {
                        // Deshabilitar controles de juego
                        _permitirAccionesJuego = false;
                        esMiTurno = false;

                        ChatPanel(true, "Sistema/¡Has sido eliminado! Ya no puedes realizar acciones.");
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

                    Debug.WriteLine($"[DESAFÍO] Carta {nombreCarta}: {(esValida ? "VÁLIDA" : "INVÁLIDA")}");
                }
            }

            // Activar la visualización y reiniciar el temporizador
            mostrandoDesafio = true;
            tiempoDesafio = 0f;
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
                        historialChat.Add($"{nombre}/{mensajeChat}");
                    }
                    else
                    {
                        // Mensaje de sistema o de otro formato
                        historialChat.Add(mensaje);
                    }
                }

                // Limpiar la interfaz anterior
                if (UserInterface.Active != null)
                {
                    UserInterface.Active.Clear();
                }
                else
                {
                    Debug.WriteLine("[ERROR] UserInterface.Active es null");
                    return;
                }

                // Crear el panel principal con altura fija
                Panel panel = new Panel(new Vector2(350, 500), PanelSkin.Simple, Anchor.CenterRight);
                if (panel == null)
                {
                    Debug.WriteLine("[ERROR] No se pudo crear el panel principal");
                    return;
                }

                panel.Padding = new Vector2(15, 15);
                panel.Visible = visible;
                UserInterface.Active.AddEntity(panel);

                // ---- ESTRUCTURA VERTICAL CLARA ----
                // 1. Encabezado + línea (altura ~40px)
                // 2. Panel de mensajes (altura fija 340px)
                // 3. Línea divisoria
                // 4. Campo de texto (altura 40px)
                // 5. Panel de botones (altura 60px)

                // 1. ENCABEZADO
                Header header = new Header("Chat");
                if (header != null) panel.AddChild(header);

                HorizontalLine lineTop = new HorizontalLine();
                if (lineTop != null) panel.AddChild(lineTop);

                // 2. PANEL DE MENSAJES - contenedor fijo
                Panel mensajesPanel = new Panel(new Vector2(0, 340), PanelSkin.None, Anchor.TopCenter);
                if (mensajesPanel == null)
                {
                    Debug.WriteLine("[ERROR] No se pudo crear el panel de mensajes");
                    return;
                }

                mensajesPanel.Padding = new Vector2(10, 10);
                panel.AddChild(mensajesPanel);

                // Limitar historial y obtener los mensajes más recientes
                int mensajesPorPantalla = 10; // Aproximadamente cuántos caben

                if (historialChat == null)
                {
                    historialChat = new List<string>();
                }

                int totalMensajes = historialChat.Count;
                int indiceInicio = Math.Max(0, totalMensajes - mensajesPorPantalla);

                // Limitar historial para no acumular demasiados
                int maxHistorialPermitido = 10;
                if (historialChat.Count > maxHistorialPermitido)
                {
                    historialChat.RemoveRange(0, historialChat.Count - maxHistorialPermitido);
                    indiceInicio = Math.Max(0, historialChat.Count - mensajesPorPantalla);
                }

                // Mostrar los mensajes más recientes
                for (int i = indiceInicio; i < totalMensajes; i++)
                {
                    if (i >= 0 && i < historialChat.Count)
                    {
                        string msg = historialChat[i];
                        if (msg == null) continue;

                        string[] partes = msg.Split('/');
                        Paragraph mensajeParagraph;

                        if (partes.Length >= 2)
                        {
                            mensajeParagraph = new Paragraph($"{partes[0]}: {partes[1]}");
                        }
                        else
                        {
                            mensajeParagraph = new Paragraph(msg);
                        }

                        if (mensajeParagraph != null)
                        {
                            mensajeParagraph.Scale = 0.9f;
                            mensajeParagraph.WrapWords = true;
                            mensajesPanel.AddChild(mensajeParagraph);
                        }
                    }
                }

                // 3. LÍNEA DIVISORIA - clara separación visual
                HorizontalLine lineMiddle = new HorizontalLine();
                if (lineMiddle != null) panel.AddChild(lineMiddle);

                // 4. CAMPO DE TEXTO - asegurando que esté disponible
                TextInput text = new TextInput(false);
                if (text != null)
                {
                    text.PlaceholderText = "Escribe un mensaje...";
                    text.Size = new Vector2(0, 40);
                    text.Anchor = Anchor.Auto; // Posición automática según el orden
                    panel.AddChild(text);
                }

                // 5. PANEL DE BOTONES - anclado abajo explícitamente 
                Panel botonesPanel = new Panel(new Vector2(0, 60), PanelSkin.None);
                if (botonesPanel != null)
                {
                    botonesPanel.Padding = new Vector2(10, 10);
                    botonesPanel.Anchor = Anchor.BottomCenter; // Anclar explícitamente abajo
                    panel.AddChild(botonesPanel);

                    // Distribuir botones horizontalmente
                    Button enviarBtn = new Button("Enviar");
                    Button cartasBtn = new Button("Pedir Cartas");

                    if (enviarBtn != null && cartasBtn != null)
                    {
                        // Distribuir los botones en el espacio disponible
                        enviarBtn.Size = new Vector2(150, 35);
                        enviarBtn.Anchor = Anchor.TopLeft;

                        cartasBtn.Size = new Vector2(150, 35);
                        cartasBtn.Anchor = Anchor.TopRight;

                        // Añadir manejadores de eventos
                        enviarBtn.OnClick = (Entity btn) =>
                        {
                            if (text == null || string.IsNullOrEmpty(text.Value)) return;

                            string mensajeEnviar = text.Value;
                            try
                            {
                                if (server != null && server.Connected && conectado)
                                {
                                    string mensajeFormato = "11/" + usuario + "/" + mensajeEnviar;
                                    byte[] msg = Encoding.ASCII.GetBytes(mensajeFormato);
                                    server.Send(msg);
                                    text.Value = "";
                                    ChatPanel(true, null);
                                }
                                else
                                {
                                    Debug.WriteLine("[CHAT] No hay conexión. Intentando reconectar...");
                                    historialChat.Add("Sistema/Reconectando al servidor...");
                                    ChatPanel(true, null);
                                    ConnectToServerIfNeeded();

                                    if (server != null && server.Connected)
                                    {
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
                        };

                        cartasBtn.OnClick = (Entity btn) =>
                        {
                            try
                            {
                                if (conectado || ConnectToServerIfNeeded())
                                {
                                    cartasBtn.Enabled = false;
                                    cartasBtn.Visible = false; // Deshabilitar botón mientras se solicitan cartas
                                    GetCards(usuario);
                                    historialChat.Add("Sistema/Solicitando nuevas cartas...");
                                    ChatPanel(true, null);
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

                        // Añadir botones al panel
                        botonesPanel.AddChild(enviarBtn);
                        botonesPanel.AddChild(cartasBtn);
                    }
                }

                Debug.WriteLine("[CHAT] Panel de chat inicializado correctamente");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en ChatPanel: {ex.Message}\n{ex.StackTrace}");
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
                Debug.WriteLine("Desconectando del servidor...");
                stopMessageListener = true; // Detener el hilo primero

                if (server != null)
                {
                    if (server.Connected)
                    {
                        try
                        {
                            int estado = this.estado(usuario, "0");
                            server.Shutdown(SocketShutdown.Send);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error al cerrar socket: {ex.Message}");
                        }
                        finally
                        {
                            server.Close();
                        }
                    }
                    server = null;
                }
                conectado = false;
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
                                    // ¡AQUÍ ESTÁ EL PROBLEMA! No debemos salir después de recibir un mensaje
                                }
                                catch (SocketException se)
                                {
                                    if (se.SocketErrorCode == SocketError.TimedOut)
                                    {
                                        continue;
                                    }
                                    Debug.WriteLine($"[RED] Error de socket: {se.Message}");
                                    Thread.Sleep(500);
                                    continue; // Seguir intentando en vez de salir
                                }

                                if (bytesReceived > 0)
                                {
                                    string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                                    Debug.WriteLine($"[RED] Mensaje recibido: {message}");

                                    // Procesar el mensaje de forma segura
                                    ProcessServerMessage(message);

                                    // IMPORTANTE: NO hacer break o return aquí
                                    // El hilo debe seguir ejecutándose para recibir más mensajes
                                }
                            }
                            else
                            {
                                // No salir del bucle, solo esperar
                                Debug.WriteLine("[RED] Socket no conectado, esperando...");
                                Thread.Sleep(1000);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Capturar cualquier excepción pero NO salir del bucle
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
                    Debug.WriteLine("[RED] Hilo de escucha finalizado. Reconexión automática iniciada...");

                    // Reiniciar el hilo automáticamente si no fue una detención intencional
                    if (!stopMessageListener)
                    {
                        // Usar un timer para reiniciar desde el hilo principal
                        System.Threading.Timer restartTimer = null;
                        restartTimer = new System.Threading.Timer((state) =>
                        {
                            Debug.WriteLine("[RED] Intentando reiniciar hilo de escucha...");
                            StartMessageListener();
                            restartTimer?.Dispose();
                        }, null, 2000, Timeout.Infinite);
                    }
                }
            });

            messageListenerThread.IsBackground = true;
            messageListenerThread.Start();
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
                string datosCartas = $"{cartasConFiltroCount}/{string.Join(",", tiposCartas)}";

                // Enviar acción al servidor
                string mensaje = $"21/{usuario}/PLAY/{datosCartas}";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine($"[ACCIÓN] Enviadas {cartasConFiltroCount} cartas: {datosCartas}");
                ChatPanel(true, $"Sistema/Has enviado {cartasConFiltroCount} carta(s)");

                // Opcional: Limpiar las cartas enviadas de tu mano
                LimpiarCartasEnviadas();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al enviar cartas: {ex.Message}");
                ChatPanel(true, $"Sistema/ERROR: {ex.Message}");
            }
        }

        private void EnviarConfirmacionEliminacion()
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
        }

        private void LimpiarCartasEnviadas()
        {
            // Crear una nueva lista para las cartas que se mantienen
            List<Texture2D> nuevasCartasDisponibles = new List<Texture2D>();
            List<bool> nuevosFiltros = new List<bool>();

            // Guardar solo las cartas que no tienen filtro (no fueron enviadas)
            for (int i = 0; i < _cartasDisponibles.Count; i++)
            {
                if (!_cartasConFiltro[i])
                {
                    nuevasCartasDisponibles.Add(_cartasDisponibles[i]);
                    nuevosFiltros.Add(false);
                }
            }

            // Actualizar las listas
            _cartasDisponibles = nuevasCartasDisponibles;
            _cartasConFiltro = nuevosFiltros;

            // Resetear el índice de carta seleccionada
            if (_cartasDisponibles.Count > 0)
                _cartaSeleccionadaIndex = 0;
            else
                _cartaSeleccionadaIndex = -1;

            Debug.WriteLine($"[CARTAS] Cartas restantes después de enviar: {_cartasDisponibles.Count}");
        }

        public override void Update(GameTime gameTime)
        {
            try
            {
                // Procesar mensajes pendientes primero
                List<string> mensajesAhora = null;
                lock (mensajesLock)
                {
                    if (hayNuevosMensajes && mensajesPendientes.Count > 0)
                    {
                        Debug.WriteLine($"[UPDATE] Encontrados {mensajesPendientes.Count} mensajes pendientes");
                        mensajesAhora = new List<string>(mensajesPendientes);
                        mensajesPendientes.Clear();
                        hayNuevosMensajes = false;
                    }
                }

                if (mensajesAhora != null)
                {
                    foreach (string msg in mensajesAhora)
                    {
                        Debug.WriteLine($"[UPDATE] Procesando mensaje pendiente: {msg}");

                        if (msg.StartsWith("CARDS/"))
                        {
                            // Procesar mensaje de cartas
                            ProcesarCartasDelServidor(msg);
                            Debug.WriteLine($"[UPDATE] Cartas procesadas, disponibles ahora: {_cartasDisponibles.Count}");
                        }
                        else if (msg.StartsWith("CHAT/"))
                        {
                            // Procesar mensaje de chat
                            string chatMessage = msg.Substring(5);
                            ChatPanel(true, chatMessage);
                        }
                    }
                }

                // Obtener estados de entrada actuales
                KeyboardState estadoTeclado = Keyboard.GetState();
                var keyboardState = KeyboardExtended.GetState();
                var mouseState = MouseExtended.GetState();

                // Permitir que UserInterface procese la entrada
                if (UserInterface.Active != null)
                {
                    // Esto es importante para que los controles de UI respondan al ratón
                    UserInterface.Active.Update(gameTime);
                }

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
                    // Incrementar el contador de tiempo
                    tiempoDesafio += (float)gameTime.ElapsedGameTime.TotalSeconds;

                    // Verificar si ha pasado el tiempo de visualización
                    if (tiempoDesafio >= DURACION_DESAFIO)
                    {
                        // Finalizar la visualización
                        mostrandoDesafio = false;

                        // Enviar confirmación al servidor para que proceda con la eliminación
                        EnviarConfirmacionEliminacion();
                    }
                }

                // Solo procesar teclas de juego si el ratón no está sobre la UI
                if (!ratónSobreUI)
                {
                    if (keyboardState.WasKeyReleased(Keys.Escape))
                    {
                        Debug.WriteLine("[INPUT] Tecla ESC presionada - Mostrando menú de pausa");
                        EscMenu(usuario, true);
                    }

                    // Tecla Q para acercar/alejar SIEMPRE disponible
                    if (keyboardState.WasKeyReleased(Keys.Q))
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

                    // Navegación con flechas SIEMPRE disponible cuando las cartas están acercadas
                    if (_cartasAcercadas)
                    {
                        if (keyboardState.WasKeyReleased(Keys.Left))
                        {
                            if (_cartasDisponibles.Count > 0)
                            {
                                _cartaSeleccionadaIndex--;
                                if (_cartaSeleccionadaIndex < 0)
                                    _cartaSeleccionadaIndex = _cartasDisponibles.Count - 1;

                                Debug.WriteLine($"[NAVEGACIÓN] Carta seleccionada: {_cartaSeleccionadaIndex}");
                            }
                        }
                        else if (keyboardState.WasKeyReleased(Keys.Right))
                        {
                            if (_cartasDisponibles.Count > 0)
                            {
                                _cartaSeleccionadaIndex = (_cartaSeleccionadaIndex + 1) % _cartasDisponibles.Count;

                                Debug.WriteLine($"[NAVEGACIÓN] Carta seleccionada: {_cartaSeleccionadaIndex}");
                            }
                        }

                        if (keyboardState.WasKeyReleased(Keys.F8))
                        {
                            _permitirAccionesJuego = true;
                            esMiTurno = true;
                            Debug.WriteLine("[FORZADO] Estado de juego forzado a: permitirAcciones=True, esMiTurno=True");
                            ChatPanel(true, "Sistema/FORZADO: Controles habilitados manualmente");
                        }

                        // GRUPO 2: TECLAS DE ACCIÓN (solo disponibles en tu turno)
                        if (_permitirAccionesJuego == true)
                        {
                            // Espacio para seleccionar/aplicar filtro a la carta
                            if (keyboardState.WasKeyReleased(Keys.Space))
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

                            // Tecla E para enviar cartas seleccionadas
                            if (keyboardState.WasKeyReleased(Keys.E))
                            {
                                EnviarCartasSeleccionadas();
                            }

                            if (keyboardState.WasKeyReleased(Keys.F))
                            {
                                // Forzar la eliminación de cartas seleccionadas
                                DesafiarJugador();
                            }
                        }
                        else if (keyboardState.WasKeyReleased(Keys.Space) || keyboardState.WasKeyReleased(Keys.E) || keyboardState.WasKeyReleased(Keys.F))
                        {
                            // Si no se permiten acciones de juego, mostrar mensaje
                            Debug.WriteLine("[INPUT] Intento de acción de juego fuera de turno");
                            {
                                ChatPanel(true, "Sistema/No puedes realizar acciones fuera de tu turno");
                            }
                        }
                    }

                    // IMPORTANTE: Actualizar el estado anterior del teclado al final
                    _estadoTecladoAnterior = keyboardState;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en Update: {ex.Message}");
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
                    float escalaActual = _cartasAcercadas ? _escalaCartasAcercadas : _escalaCartasNormal;
                    Vector2 posicionBase = _cartasAcercadas ? _posicionCartasAcercadas : _posicionCartasNormal;

                    // Recalcular posición base para centrar basado en la cantidad de cartas
                    float anchoTotal = _cartasDisponibles.Count * (150 * escalaActual * 0.8f); // 0.8 para espaciado
                    float posX = GraphicsDevice.Viewport.Width / 2 - anchoTotal / 2;

                    for (int i = 0; i < _cartasDisponibles.Count; i++)
                    {
                        if (_cartasDisponibles[i] != null)
                        {
                            int x = (int)(posX + (i * 150 * escalaActual * 0.8f));
                            int y = _cartasAcercadas ? GraphicsDevice.Viewport.Height / 2 - 112
                                                     : GraphicsDevice.Viewport.Height - 150;

                            int ancho = (int)(150 * escalaActual);
                            int alto = (int)(225 * escalaActual);


                            // Determinar el color a aplicar
                            Color colorTextura = Color.White;

                            // Si esta carta tiene filtro aplicado
                            if (_cartasConFiltro.Count > i && _cartasConFiltro[i])
                            {
                                colorTextura = _colorFiltro;
                            }

                            // Si esta es la carta seleccionada (para el borde amarillo)
                            if (i == _cartaSeleccionadaIndex && _cartasAcercadas)
                            {
                                // Dibujar un borde alrededor de la carta seleccionada
                                Texture2D bordeTextura = GetOrCreatePlainTexture(Color.Yellow);
                                int bordeGrosor = 3;

                                // Borde superior
                                _spriteBatch.Draw(bordeTextura, new Rectangle(x - bordeGrosor, y - bordeGrosor, ancho + bordeGrosor * 2, bordeGrosor), Color.Yellow);
                                // Borde inferior
                                _spriteBatch.Draw(bordeTextura, new Rectangle(x - bordeGrosor, y + alto, ancho + bordeGrosor * 2, bordeGrosor), Color.Yellow);
                                // Borde izquierdo
                                _spriteBatch.Draw(bordeTextura, new Rectangle(x - bordeGrosor, y - bordeGrosor, bordeGrosor, alto + bordeGrosor * 2), Color.Yellow);
                                // Borde derecho
                                _spriteBatch.Draw(bordeTextura, new Rectangle(x + ancho, y - bordeGrosor, bordeGrosor, alto + bordeGrosor * 2), Color.Yellow);
                            }

                            // Dibujar la carta con el color apropiado (filtrado o no)
                            _spriteBatch.Draw(
                                _cartasDisponibles[i],
                                new Rectangle(x, y, ancho, alto),
                                colorTextura
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

                if (mostrandoDesafio)
                {
                    // Dibujar un fondo semitransparente
                    Texture2D fondoTextura = GetOrCreatePlainTexture(new Color(0, 0, 0, 180));
                    _spriteBatch.Draw(fondoTextura, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);

                    // Si hay cartas para mostrar
                    if (cartasDesafio.Count > 0)
                    {
                        // Calcular posición central para las cartas
                        int anchoTotal = cartasDesafio.Count * 160; // 150px por carta + 10px espacio
                        int inicioX = GraphicsDevice.Viewport.Width / 2 - anchoTotal / 2;
                        int posY = GraphicsDevice.Viewport.Height / 2 - 150; // Centrado vertical

                        // Dibujar cada carta con su filtro
                        for (int i = 0; i < cartasDesafio.Count; i++)
                        {
                            int posX = inicioX + (i * 160);

                            // Determinar color de filtro según validez
                            Color colorFiltro = cartasDesafioValidas[i] ?
                                new Color(0, 255, 0, 100) :  // Verde semitransparente
                                new Color(255, 0, 0, 100);   // Rojo semitransparente

                            // Dibujar la carta
                            _spriteBatch.Draw(
                                cartasDesafio[i],
                                new Rectangle(posX, posY, 150, 225),
                                Color.White
                            );

                            // Dibujar el filtro
                            Texture2D filtroTextura = GetOrCreatePlainTexture(colorFiltro);
                            _spriteBatch.Draw(
                                filtroTextura,
                                new Rectangle(posX, posY, 150, 225),
                                colorFiltro
                            );
                        }
                    }
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

        // Método para enviar acciones al servidor
        private void EnviarAccionJuego(string accion, string datos)
        {
            try
            {
                if (!esMiTurno)
                {
                    ChatPanel(true, "Sistema/No puedes realizar acciones fuera de tu turno");
                    return;
                }

                // Formato correcto para el servidor: 21/usuario/accion/datos
                string mensaje = $"21/{usuario}/{accion}/{datos}";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine($"[ACCIÓN] Enviada: {accion} {datos}");

                // Opcional: deshabilitar los controles inmediatamente mientras se procesa la acción
                // para evitar acciones duplicadas
                _permitirAccionesJuego = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al enviar acción: {ex.Message}");
                ChatPanel(true, $"Sistema/ERROR: {ex.Message}");
            }
        }
    }
}