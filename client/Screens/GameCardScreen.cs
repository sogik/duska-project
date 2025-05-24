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
        public int grupoId;

        private RichParagraph panelmensajes = new RichParagraph(@"");

        // Añade estas variables para manejar mensajes pendientes
        private List<string> mensajesPendientes = new List<string>();
        private object mensajesLock = new object();
        private bool hayNuevosMensajes = false;

        // Variable para controlar la frecuencia de los mensajes de depuración
        private int _drawCounter = 0;

        // Añade esta variable de clase para mantener el historial de mensajes
        private List<string> historialChat = new List<string>();

        // Variables para animación de cartas
        private List<int> _cartasJugando = new List<int>();
        private List<Vector2> _posicionesIniciales = new List<Vector2>();
        private Vector2 _posicionCentroMesa = Vector2.Zero;
        private float _tiempoAnimacion = 0f;
        private bool _animacionEnCurso = false;
        private const float DURACION_ANIMACION = 1.5f; // Duración en segundos

        // Variables para cartas en el centro
        private List<Texture2D> _cartasEnCentro = new List<Texture2D>();
        private List<Color> _filtrosCartasCentro = new List<Color>();
        private int _cantidadCartasCentro = 0;

        // Variables de posición (si no existen)
        private Vector2 _posicionCartasNormal;
        private Vector2 _posicionCartasAcercadas;
        private bool _cartasAcercadas = false;

        // Añadir estas variables al inicio de la clase
        private float _tiempoVerificacionHilo = 0f;
        private const float INTERVALO_VERIFICACION = 5f;

        private List<bool> _cartasConFiltro = new List<bool>();
        private int _cartaSeleccionadaIndex = -1;
        private Color _colorFiltro = new Color(255, 255, 0, 100); // Filtro amarillo semi-transparente

        // Variables de escala para las cartas
        private float _escalaCartasNormal = 0.8f;
        private float _escalaCartasAcercadas = 1.0f;
        private float _escalaCentroMesa = 0.5f;

        // Variables para la revelación de cartas en el centro
        private bool _mostrandoRevelacion = false;
        private float _tiempoRevelacion = 0f;

        public GameCardScreen(Game game, string usuario, int grupo) : base(game)
        {
            this.usuario = usuario;
            _cartasDisponibles = new List<Texture2D>();
            _cartas = new Texture2D[5];
            _random = new Random();
            _mostrarTodas = true;
            grupoId = grupo;
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
                    StartMessageListener();
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
        private void ProcessServerMessage(string msg)
        {
            try
            {
                Debug.WriteLine($"[SERVER] Procesando mensaje: {msg}");

                if (msg.StartsWith("CARDS/"))
                {
                    Debug.WriteLine($"[SERVER] *** MENSAJE DE CARTAS RECIBIDO *** : {msg}");

                    // Solo procesa o encola, pero no ambos
                    if (System.Threading.Thread.CurrentThread.IsBackground)
                    {
                        // Si estamos en un hilo background, encola para procesamiento en hilo principal
                        lock (mensajesLock)
                        {
                            mensajesPendientes.Add(msg);
                            hayNuevosMensajes = true;
                            Debug.WriteLine("[SERVER] Mensaje añadido a la cola para procesamiento");
                        }
                    }
                    else
                    {
                        // Si estamos en el hilo principal, procesa directamente
                        ProcesarCartasDelServidor(msg);
                        // Forzar actualización inmediata
                        _mostrarTodas = true;
                    }
                }
                else if (msg.StartsWith("CHAT/"))
                {
                    // Mensaje de chat
                    string chatMessage = msg.Substring(5);
                    Debug.WriteLine("Mensaje de chat recibido: " + chatMessage);

                    // Usar Invoke para actualizar UI desde cualquier hilo
                    if (System.Threading.Thread.CurrentThread.IsBackground)
                    {
                        // Llamada segura a método de UI
                        // En MonoGame normalmente necesitarías una forma de ejecutar esto en el hilo principal
                        lock (mensajesLock)
                        {
                            mensajesPendientes.Add(msg);
                            hayNuevosMensajes = true;
                        }
                    }
                    else
                    {
                        ChatPanel(true, chatMessage);
                    }
                }
                else if (msg.StartsWith("MESA/"))
                {
                    string tipoMesa = msg.Substring(5);
                    Debug.WriteLine($"[MESA] Recibido tipo de mesa: {tipoMesa}");
                    // Actualizar interfaz según tipo de mesa
                }
                else if (msg.StartsWith("JUGADA/"))
                {
                    // Parse jugada data
                    string[] parts = msg.Substring(7).Split('/');
                    int jugadorId = int.Parse(parts[0]);
                    int grupoId = int.Parse(parts[1]);
                    int tipoCarta = int.Parse(parts[2]);
                    int valorCarta = int.Parse(parts[3]);

                    Debug.WriteLine($"[JUEGO] Jugador {jugadorId} jugó carta tipo {tipoCarta} valor {valorCarta}");
                    // Actualizar interfaz con la nueva jugada
                }
                else if (msg.StartsWith("RETO/"))
                {
                    // Parse reto data
                    string[] parts = msg.Substring(5).Split('/');
                    int retador = int.Parse(parts[0]);
                    int retado = int.Parse(parts[1]);
                    int eliminado = int.Parse(parts[2]);

                    Debug.WriteLine($"[JUEGO] Reto: {retador} retó a {retado}, eliminado: {eliminado}");
                    // Actualizar interfaz con el resultado del reto
                }
                else if (msg.StartsWith("GANADOR/"))
                {
                    int ganador = int.Parse(msg.Substring(8));
                    Debug.WriteLine($"[JUEGO] ¡El ganador es el jugador {ganador}!");
                    // Mostrar pantalla de ganador
                }
                else if (msg.StartsWith("ACUSACION/"))
                {
                    string[] partes = msg.Substring(10).Split('/');
                    if (partes.Length >= 3)
                    {
                        int acusador = int.Parse(partes[0]);
                        int acusado = int.Parse(partes[1]);
                        int resultado = int.Parse(partes[2]);

                        // resultado: 1 = mentira detectada, 2 = era verdad
                        bool eranVerdaderas = (resultado == 2);

                        // Revelar las cartas con el color correspondiente
                        RevelarCartasCentro(eranVerdaderas);

                        string mensaje = eranVerdaderas ?
                            $"¡VERDAD! El jugador {acusado} no mentía. El jugador {acusador} pierde." :
                            $"¡MENTIRA! El jugador {acusado} estaba mintiendo. El jugador {acusado} pierde.";

                        //MostrarMensajeAccion(mensaje);
                        Debug.WriteLine($"[JUEGO] {mensaje}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVER] Error procesando mensaje: {ex.Message}");
            }
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

        // Modificar el método que procesa las cartas recibidas
        private void ProcesarCartasRecibidas(string cartasData)
        {
            Debug.WriteLine($"[CARTAS] *** PROCESANDO CARTAS (RECIBIDAS) ***: {cartasData}");

            try
            {
                string[] partes = cartasData.Split('/');

                // Verificar que tenemos el formato correcto
                if (partes.Length < 5 || !partes[0].Equals("CARDS", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[CARTAS] Formato incorrecto (recibidas): {cartasData}");
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
                            Debug.WriteLine($"[CARTAS] Tipo {i} ({GetNombreCarta(i)}): {cantidad} cartas (recibidas)");

                            // Verificar textura no nula
                            if (_cartas[i] == null)
                            {
                                Debug.WriteLine($"[CARTAS] ¡ERROR! La textura para {GetNombreCarta(i)} es nula (recibidas)");
                                continue;
                            }

                            for (int j = 0; j < cantidad; j++)
                            {
                                cartasTemp.Add(_cartas[i]);
                                totalCartas++;
                                Debug.WriteLine($"[CARTAS] Añadida carta {GetNombreCarta(i)} #{j + 1} (recibidas)");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[CARTAS] ¡ERROR! Índice {i} fuera de rango para _cartas[] (recibidas)");
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
                            Debug.WriteLine($"[CARTAS] Añadida carta de respaldo #{i + 1} (recibidas)");
                        }
                        else
                        {
                            Debug.WriteLine("[CARTAS] ¡ERROR! La carta de respaldo es nula (recibidas)");
                        }
                    }
                }

                // Mostrar detalles de las cartas antes de actualizar
                Debug.WriteLine($"[CARTAS] Cartas a añadir (recibidas): {cartasTemp.Count}");
                for (int i = 0; i < cartasTemp.Count; i++)
                {
                    string nombreCarta = GetNombreCartaPorTextura(cartasTemp[i]);
                    Debug.WriteLine($"[CARTAS] Carta {i}: {nombreCarta} (recibidas)");
                }

                // Actualizar la lista real de forma segura
                _cartasDisponibles = new List<Texture2D>(cartasTemp);

                // Asegurarse de actualizar los filtros
                _cartasConFiltro = new List<bool>(_cartasDisponibles.Count);
                for (int i = 0; i < _cartasDisponibles.Count; i++)
                    _cartasConFiltro.Add(false);

                Debug.WriteLine($"[CARTAS] *** ACTUALIZACIÓN COMPLETA (RECIBIDAS) ***. Total cartas: {_cartasDisponibles.Count}");

                // Actualizar posiciones
                ActualizarPosicionesCartas();

                // Forzar redibujado
                _mostrarTodas = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CARTAS] Error crítico (recibidas): {ex.Message}\n{ex.StackTrace}");
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
            // Este método se llama cuando las cartas cambian de posición
            // Ya implementado en código existente, pero asegurar que use las variables correctas

            for (int i = 0; i < _cartasDisponibles.Count; i++)
            {
                // Las posiciones se calculan dinámicamente en el Draw()
                // Este método puede estar vacío o hacer cálculos adicionales si es necesario
            }
        }

        private void GetCards(string usuario)
        {
            try
            {
                // Verificar que el hilo esté funcionando antes de enviar
                if (messageListenerThread == null || !messageListenerThread.IsAlive)
                {
                    Debug.WriteLine("[CARTAS] Hilo de escucha no activo. Reiniciando...");
                    StartMessageListener();
                    Thread.Sleep(500); // Dar tiempo para que se inicie
                }

                // Verificar conexión
                if (!conectado || server == null || !server.Connected)
                {
                    Debug.WriteLine("[CARTAS] Error: No hay conexión disponible para solicitar cartas.");
                    return;
                }

                string mensaje = "9/" + usuario + "/";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine("[CARTAS] Solicitud manual de cartas enviada");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ERROR] Error al solicitar cartas: " + ex.Message);

                // Intentar reconectar si hay error
                ReconnectToServer();
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

        // Reemplaza el método StartMessageListener con esta versión mejorada
        private void StartMessageListener()
        {
            stopMessageListener = false;
            Debug.WriteLine("[RED] Iniciando hilo de escucha de mensajes...");

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
                                // Configurar timeout para evitar bloqueos indefinidos
                                server.ReceiveTimeout = 1000; // 1 segundo

                                byte[] buffer = new byte[1024];
                                int bytesReceived = 0;

                                try
                                {
                                    bytesReceived = server.Receive(buffer);
                                }
                                catch (SocketException se)
                                {
                                    // Si es timeout, simplemente continuar el bucle
                                    if (se.SocketErrorCode == SocketError.TimedOut)
                                    {
                                        continue; // NO salir del bucle, solo continuar
                                    }

                                    // Para otros errores de socket, registrar pero continuar
                                    Debug.WriteLine($"[RED] Error de socket: {se.Message}");
                                    Thread.Sleep(500);
                                    continue; // Continuar intentando, NO salir
                                }

                                if (bytesReceived > 0)
                                {
                                    string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                                    Debug.WriteLine($"[RED] Mensaje recibido: {message}");

                                    // Procesar el mensaje de forma segura
                                    ProcessServerMessage(message);

                                    // CRÍTICO: NO hacer break, return o salir aquí
                                    // El hilo debe seguir ejecutándose para recibir más mensajes
                                }
                                else
                                {
                                    // Si no se recibieron datos, continuar
                                    Thread.Sleep(100);
                                }
                            }
                            else
                            {
                                // Socket no conectado, intentar reconectar
                                Debug.WriteLine("[RED] Socket desconectado, intentando reconectar...");
                                Thread.Sleep(2000);

                                // Intentar reconexión automática
                                if (!isReconnecting)
                                {
                                    ReconnectToServer();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Capturar cualquier excepción pero NO salir del bucle
                            Debug.WriteLine($"[RED] Error en bucle de escucha: {ex.Message}");
                            Thread.Sleep(1000);
                            // Continuar el bucle, NO hacer break o return
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
                    Debug.WriteLine("[RED] Hilo de mensajes se ha detenido.");

                    // Reiniciar automáticamente si no fue detención intencional
                    if (!stopMessageListener)
                    {
                        Debug.WriteLine("[RED] Reiniciando hilo automáticamente en 3 segundos...");

                        // Usar un timer para reiniciar desde el hilo principal
                        System.Threading.Timer restartTimer = null;
                        restartTimer = new System.Threading.Timer((state) =>
                        {
                            if (!stopMessageListener && !isReconnecting)
                            {
                                Debug.WriteLine("[RED] Ejecutando reinicio automático del hilo...");
                                StartMessageListener();
                            }
                            restartTimer?.Dispose();
                        }, null, 3000, Timeout.Infinite);
                    }
                }
            });

            messageListenerThread.IsBackground = true;
            messageListenerThread.Start();
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
                        else if (msg.StartsWith("MESA/"))
                        {
                            string tipoMesa = msg.Substring(5);
                            Debug.WriteLine($"[MESA] Recibido tipo de mesa: {tipoMesa}");
                            // Actualizar interfaz según tipo de mesa
                        }
                        else if (msg.StartsWith("JUGADA/"))
                        {
                            // Parse jugada data
                            string[] parts = msg.Substring(7).Split('/');
                            int jugadorId = int.Parse(parts[0]);
                            int grupoId = int.Parse(parts[1]);
                            int tipoCarta = int.Parse(parts[2]);
                            int valorCarta = int.Parse(parts[3]);

                            Debug.WriteLine($"[JUEGO] Jugador {jugadorId} jugó carta tipo {tipoCarta} valor {valorCarta}");
                            // Actualizar interfaz con la nueva jugada
                        }
                        else if (msg.StartsWith("RETO/"))
                        {
                            // Parse reto data
                            string[] parts = msg.Substring(5).Split('/');
                            int retador = int.Parse(parts[0]);
                            int retado = int.Parse(parts[1]);
                            int eliminado = int.Parse(parts[2]);

                            Debug.WriteLine($"[JUEGO] Reto: {retador} retó a {retado}, eliminado: {eliminado}");
                            // Actualizar interfaz con el resultado del reto
                        }
                        else if (msg.StartsWith("GANADOR/"))
                        {
                            int ganador = int.Parse(msg.Substring(8));
                            Debug.WriteLine($"[JUEGO] ¡El ganador es el jugador {ganador}!");
                            // Mostrar pantalla de ganador
                        }
                        else if (msg.StartsWith("ACUSACION/"))
                        {
                            string[] partes = msg.Substring(10).Split('/');
                            if (partes.Length >= 3)
                            {
                                int acusador = int.Parse(partes[0]);
                                int acusado = int.Parse(partes[1]);
                                int resultado = int.Parse(partes[2]);

                                // resultado: 1 = mentira detectada, 2 = era verdad
                                bool eranVerdaderas = (resultado == 2);

                                // Revelar las cartas con el color correspondiente
                                RevelarCartasCentro(eranVerdaderas);

                                string mensaje = eranVerdaderas ?
                                    $"¡VERDAD! El jugador {acusado} no mentía. El jugador {acusador} pierde." :
                                    $"¡MENTIRA! El jugador {acusado} estaba mintiendo. El jugador {acusado} pierde.";

                                //MostrarMensajeAccion(mensaje);
                                Debug.WriteLine($"[JUEGO] {mensaje}");
                            }
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

                // Solo procesar teclas de juego si el ratón no está sobre la UI
                if (!ratónSobreUI)
                {
                    // Detectar tecla Q para acercar/alejar las cartas
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

                    // La navegación con flechas y aplicación de filtros SOLO funciona cuando las cartas están acercadas
                    if (_cartasAcercadas)
                    {
                        // Navegación con flechas entre cartas
                        if (keyboardState.WasKeyReleased(Keys.Left))
                        {
                            if (_cartasDisponibles.Count > 0)
                            {
                                _cartaSeleccionadaIndex--;
                                if (_cartaSeleccionadaIndex < 0)
                                    _cartaSeleccionadaIndex = _cartasDisponibles.Count - 1;

                                // ELIMINAR: _filtroAplicado = false; 
                                // El filtro ahora permanece activo al cambiar de carta
                                Debug.WriteLine($"[NAVEGACIÓN] Carta seleccionada: {_cartaSeleccionadaIndex} | Filtro: {(_cartasConFiltro[_cartaSeleccionadaIndex] ? "activo" : "inactivo")}");
                            }
                        }
                        else if (keyboardState.WasKeyReleased(Keys.Right))
                        {
                            if (_cartasDisponibles.Count > 0)
                            {
                                _cartaSeleccionadaIndex = (_cartaSeleccionadaIndex + 1) % _cartasDisponibles.Count;

                                // ELIMINAR: _filtroAplicado = false; 
                                // El filtro ahora permanece activo al cambiar de carta

                            }
                        }

                        // Espacio ahora alterna el filtro en la carta seleccionada
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
                    }

                    // Añadir en el método Update, dentro del bloque if (!ratónSobreUI)
                    if (keyboardState.WasKeyReleased(Keys.Escape))
                    {
                        Debug.WriteLine("[INPUT] Tecla ESC presionada - Mostrando menú de pausa");
                        EscMenu(usuario, true);
                    }

                    // Añade esto dentro del método Update
                    if (keyboardState.WasKeyReleased(Keys.E) && !_animacionEnCurso)
                    {
                        // Verificar si hay cartas con filtro
                        List<int> cartasSeleccionadas = new List<int>();
                        for (int i = 0; i < _cartasConFiltro.Count; i++)
                        {
                            if (_cartasConFiltro[i])
                                cartasSeleccionadas.Add(i);
                        }

                        // Si hay cartas seleccionadas, enviarlas como jugada
                        if (cartasSeleccionadas.Count > 0)
                        {
                            try
                            {
                                // Crear lista de tipos de cartas
                                List<int> tipoCartas = new List<int>();
                                foreach (int indice in cartasSeleccionadas)
                                {
                                    if (indice >= 0 && indice < _cartasDisponibles.Count)
                                    {
                                        int tipoCarta = DeterminarTipoCarta(_cartasDisponibles[indice]);
                                        tipoCartas.Add(tipoCarta);
                                    }
                                }

                                string tiposString = string.Join("/", tipoCartas);
                                string mensaje = $"15/{usuario}/{grupoId}/{tipoCartas.Count}/{tiposString}";

                                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                                server.Send(msg);

                                Debug.WriteLine($"[JUGADA] Enviadas {tipoCartas.Count} cartas: {tiposString}");

                                // Iniciar animación para todas las cartas seleccionadas
                                IniciarAnimacionJugada(cartasSeleccionadas);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[ERROR] Error al enviar jugada: {ex.Message}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[JUGADA] No hay cartas seleccionadas para jugar");
                        }
                    }

                    // Si hay una animación en curso, actualizarla
                    if (_animacionEnCurso)
                    {
                        ActualizarAnimacionJugada(gameTime);
                    }
                }

                // Verificar salud del hilo de escucha cada 5 segundos
                _tiempoVerificacionHilo += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_tiempoVerificacionHilo >= INTERVALO_VERIFICACION)
                {
                    _tiempoVerificacionHilo = 0f;
                    VerificarSaludHilo();
                }

                // IMPORTANTE: Actualizar el estado anterior del teclado al final
                _estadoTecladoAnterior = keyboardState;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error en Update: {ex.Message}");
            }
        }

        // Método para verificar la salud del hilo
        private void VerificarSaludHilo()
        {
            if (conectado && (messageListenerThread == null || !messageListenerThread.IsAlive))
            {
                Debug.WriteLine("[RED] ¡Hilo de escucha muerto detectado! Reiniciando...");
                StartMessageListener();
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

                // Dibujar las cartas que están siendo animadas (en juego)
                if (_animacionEnCurso)
                {
                    // Factor de progreso de la animación (0.0 a 1.0)
                    float progreso = _tiempoAnimacion / DURACION_ANIMACION;

                    // Aplicar curva de animación suave (ease-in-out)
                    progreso = (float)(Math.Sin(progreso * Math.PI - Math.PI / 2) * 0.5f + 0.5f);

                    for (int i = 0; i < _cartasJugando.Count; i++)
                    {
                        int indiceCarta = _cartasJugando[i];

                        if (indiceCarta >= 0 && indiceCarta < _cartasDisponibles.Count)
                        {
                            // Calcular posición interpolada hacia el centro de la mesa
                            Vector2 posInicial = _posicionesIniciales[i];
                            Vector2 posObjetivo = _posicionCentroMesa;
                            Vector2 nuevaPosicion = Vector2.Lerp(posInicial, posObjetivo, progreso);

                            // Calcular escala interpolada
                            float escalaActual = MathHelper.Lerp(_escalaCartasNormal, _escalaCartasAcercadas, progreso);

                            // Dibujar carta en nueva posición y escala
                            _spriteBatch.Draw(
                                _cartasDisponibles[indiceCarta],
                                nuevaPosicion,
                                null,
                                Color.White,
                                0f,
                                new Vector2(75, 112.5f),
                                escalaActual,
                                SpriteEffects.None,
                                0f
                            );
                        }
                    }
                }

                // Dibujar las cartas en el centro de la mesa
                for (int i = 0; i < _cartasEnCentro.Count; i++)
                {
                    // Calcular posición para distribuir las cartas en el centro
                    float offsetX = (i - (_cantidadCartasCentro - 1) / 2.0f) * 60;
                    Vector2 posicion = new Vector2(_posicionCentroMesa.X + offsetX, _posicionCentroMesa.Y);

                    // Dibujar la carta con su filtro correspondiente
                    _spriteBatch.Draw(
                        _cartasEnCentro[i],
                        posicion,
                        null,
                        _filtrosCartasCentro[i],
                        0f, // Sin rotación
                        new Vector2(_cartasEnCentro[i].Width / 2, _cartasEnCentro[i].Height / 2), // Origen centro
                        _escalaCentroMesa, // Escala más pequeña para el centro
                        SpriteEffects.None,
                        0
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

        private void CrearMesa()
        {
            if (server != null && server.Connected)
            {
                string mensaje = "14/" + usuario + "/" + grupoId.ToString();
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);
            }
        }

        private void RealizarJugada(int jugadorId, int tipoCarta, int valorCarta)
        {
            if (server != null && server.Connected)
            {
                string mensaje = "15/" + usuario + "/" + grupoId.ToString() + "/" +
                                 jugadorId.ToString() + "/" + tipoCarta.ToString() + "/" + valorCarta.ToString();
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);
            }
        }

        private void RetarJugador(int jugadorReta, int jugadorRetado)
        {
            if (server != null && server.Connected)
            {
                string mensaje = "16/" + usuario + "/" + grupoId.ToString() + "/" +
                                 jugadorReta.ToString() + "/" + jugadorRetado.ToString();
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);
            }
        }

        private void EnviarCartasComoJugada(List<int> cartasSeleccionadas)
        {
            try
            {
                if (!conectado || server == null || !server.Connected)
                {
                    Debug.WriteLine("[JUGADA] Error: No hay conexión disponible");
                    return;
                }

                // Crear lista de tipos de cartas
                List<int> tipoCartas = new List<int>();
                foreach (int indice in cartasSeleccionadas)
                {
                    if (indice >= 0 && indice < _cartasDisponibles.Count)
                    {
                        int tipoCarta = DeterminarTipoCarta(_cartasDisponibles[indice]);
                        tipoCartas.Add(tipoCarta);
                        Debug.WriteLine($"[CARTAS] Carta {indice}: tipo {tipoCarta}");
                    }
                }

                // Formato mensaje: 15/usuario/cantidad/tipo1/tipo2/.../tipoN
                string tiposString = string.Join("/", tipoCartas);
                string mensaje = $"15/{usuario}/{tipoCartas.Count}/{tiposString}";

                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine($"[JUGADA] Enviadas {tipoCartas.Count} cartas: tipos [{tiposString}]");

                // Iniciar animación para todas las cartas seleccionadas
                IniciarAnimacionJugada(cartasSeleccionadas);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al enviar jugada: {ex.Message}");
            }
        }

        // Función para acusar de mentir (actualizada)
        private void AcusarDeMentir()
        {
            if (!conectado || server == null || !server.Connected)
            {
                Debug.WriteLine("[ACUSACIÓN] Error: No hay conexión disponible");
                return;
            }

            try
            {
                // Formato simplificado: 16/usuario/
                string mensaje = $"16/{usuario}/";
                byte[] msg = Encoding.ASCII.GetBytes(mensaje);
                server.Send(msg);

                Debug.WriteLine("[ACUSACIÓN] Enviada acusación de mentira");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error al enviar acusación: {ex.Message}");
            }
        }

        // Mostrar las instrucciones actualizadas en pantalla
        private string GetInstrucciones()
        {
            return @"CONTROLES:
- Flechas: Navegar entre cartas
- ESPACIO: Marcar/desmarcar carta
- E: Jugar cartas marcadas  
- F: Acusar de mentir al último jugador
- T: Consultar tipo de carta de la mesa

REGLAS:
- Cada mesa requiere un tipo específico de carta
- Puedes mentir si no tienes el tipo correcto
- Si te pillan mintiendo: pierdes
- Si acusas incorrectamente: pierdes";
        }

        // En el constructor o Initialize de GameCardScreen
        private void InicializarLogging()
        {
            Debug.WriteLine("=== INICIO DE SESIÓN DE DUSKA ===");
            Debug.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Debug.WriteLine($"Usuario: {usuario}");
            Debug.WriteLine("================================");
        }

        private int DeterminarTipoCarta(Texture2D textura)
        {
            // Determinar tipo basado en el array de cartas cargadas
            for (int i = 0; i < _cartas.Length; i++)
            {
                if (_cartas[i] == textura)
                {
                    return i; // 0=AS, 1=REINA, 2=REY, 3=JOKER
                }
            }

            // Si no se encuentra en el array, intentar por nombre de textura
            string nombreTextura = textura.Name?.ToLower() ?? "";

            if (nombreTextura.Contains("as") || nombreTextura.Contains("ace"))
                return 0; // CARD_AS
            if (nombreTextura.Contains("reina") || nombreTextura.Contains("queen"))
                return 1; // CARD_REINA
            if (nombreTextura.Contains("rey") || nombreTextura.Contains("king"))
                return 2; // CARD_REY
            if (nombreTextura.Contains("joker"))
                return 3; // CARD_JOKER

            // Por defecto, devolver AS
            Debug.WriteLine($"[ADVERTENCIA] No se pudo determinar tipo de carta para: {nombreTextura}");
            return 0;
        }

        private void ActualizarAnimacionJugada(GameTime gameTime)
        {
            if (!_animacionEnCurso) return;

            // Actualizar tiempo de animación
            _tiempoAnimacion += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Verificar si la animación ha terminado
            if (_tiempoAnimacion >= DURACION_ANIMACION)
            {
                // Animación completa, eliminar las cartas de la mano
                EliminarCartasJugadas();
                _animacionEnCurso = false;
                Debug.WriteLine("[ANIMACIÓN] Animación de jugada completada");
                return;
            }

            // La animación sigue en curso
            // Se renderizará en el método Draw
        }

        private void EliminarCartasJugadas()
        {
            // Ordenar índices de mayor a menor para evitar problemas al eliminar
            _cartasJugando.Sort((a, b) => b.CompareTo(a));

            // Eliminar las cartas de la mano del jugador
            foreach (int indice in _cartasJugando)
            {
                if (indice >= 0 && indice < _cartasDisponibles.Count)
                {
                    _cartasDisponibles.RemoveAt(indice);

                    if (indice < _cartasConFiltro.Count)
                        _cartasConFiltro.RemoveAt(indice);
                }
            }

            // Reiniciar selección
            _cartaSeleccionadaIndex = -1;

            Debug.WriteLine($"[JUGADA] Cartas jugadas eliminadas. Quedan {_cartasDisponibles.Count} cartas");
        }

        private Vector2 CalcularPosicionCartaNormal(int indice)
        {
            int espacioEntreCartas = 150;
            float x = _posicionCartasNormal.X + indice * espacioEntreCartas;
            float y = _posicionCartasNormal.Y;
            return new Vector2(x, y);
        }

        private Vector2 CalcularPosicionCartaAcercada(int indice)
        {
            int espacioEntreCartas = 180;
            float x = _posicionCartasAcercadas.X + indice * espacioEntreCartas;
            float y = _posicionCartasAcercadas.Y;
            return new Vector2(x, y);
        }

        private void IniciarAnimacionJugada(List<int> cartasSeleccionadas)
        {
            if (cartasSeleccionadas == null || cartasSeleccionadas.Count == 0)
            {
                Debug.WriteLine("[ANIMACIÓN] No hay cartas para animar");
                return;
            }

            Debug.WriteLine($"[ANIMACIÓN] Iniciando animación de {cartasSeleccionadas.Count} cartas");

            // Limpiar datos de animación anterior
            _cartasJugando.Clear();
            _posicionesIniciales.Clear();

            // Definir el centro de la mesa si aún no está definido
            if (_posicionCentroMesa == Vector2.Zero)
            {
                _posicionCentroMesa = new Vector2(
                    GraphicsDevice.Viewport.Width / 2,
                    GraphicsDevice.Viewport.Height / 2 - 50
                );
            }

            // Guardar las cartas que se van a animar y sus posiciones iniciales
            foreach (int indice in cartasSeleccionadas)
            {
                if (indice >= 0 && indice < _cartasDisponibles.Count)
                {
                    _cartasJugando.Add(indice);

                    // Calcular posición inicial de la carta
                    Vector2 posInicial = _cartasAcercadas ?
                        CalcularPosicionCartaAcercada(indice) :
                        CalcularPosicionCartaNormal(indice);

                    _posicionesIniciales.Add(posInicial);

                    Debug.WriteLine($"[ANIMACIÓN] Carta {indice} desde posición {posInicial}");
                }
            }

            // Guardar las texturas de las cartas para mostrarlas en el centro después
            _cartasEnCentro.Clear();
            _filtrosCartasCentro.Clear();

            foreach (int indice in cartasSeleccionadas)
            {
                if (indice >= 0 && indice < _cartasDisponibles.Count)
                {
                    _cartasEnCentro.Add(_cartasDisponibles[indice]);
                    _filtrosCartasCentro.Add(Color.White); // Sin filtro inicialmente
                }
            }

            // Inicializar parámetros de animación
            _tiempoAnimacion = 0f;
            _animacionEnCurso = true;

            Debug.WriteLine($"[ANIMACIÓN] Animación iniciada con {_cartasJugando.Count} cartas hacia el centro");
        }

        private void RevelarCartasCentro(bool sonVerdaderas)
        {
            if (_cartasEnCentro.Count == 0)
            {
                Debug.WriteLine("[REVELACIÓN] No hay cartas en el centro para revelar");
                return;
            }

            Color filtro = sonVerdaderas ?
                new Color(0, 255, 0, 180) :    // Verde para verdaderas
                new Color(255, 0, 0, 180);     // Rojo para falsas

            for (int i = 0; i < _filtrosCartasCentro.Count; i++)
            {
                _filtrosCartasCentro[i] = filtro;
            }

            _mostrandoRevelacion = true;
            _tiempoRevelacion = 0f;

            Debug.WriteLine($"[REVELACIÓN] Cartas reveladas: {(sonVerdaderas ? "VERDADERAS (Verde)" : "FALSAS (Rojo)")}");
        }

        private List<int> ObtenerCartasSeleccionadas()
        {
            List<int> cartasSeleccionadas = new List<int>();

            for (int i = 0; i < _cartasConFiltro.Count; i++)
            {
                if (_cartasConFiltro[i])
                {
                    cartasSeleccionadas.Add(i);
                }
            }

            return cartasSeleccionadas;
        }
    }
}
