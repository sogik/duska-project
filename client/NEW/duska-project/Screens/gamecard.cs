using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Screens;
using MonoGame.Extended.BitmapFonts;

namespace Duska.Screens
{
    public class gamecardScreen : GameScreen
    {
        // Texturas de cartas
        private Texture2D[] _cartas = new Texture2D[4];
        private Texture2D _cartaSeleccionada;
        
        // Estados del juego
        private bool _mostrarTodas = true;
        private Random _random = new Random();
        private SpriteBatch _spriteBatch;
        private BitmapFont _fuente;
        
        // Control de entrada
        private KeyboardState _estadoTecladoAnterior;
        private readonly string _usuario;

        // Nueva variable para almacenar todas las cartas disponibles
        private List<Texture2D> _cartasDisponibles = new List<Texture2D>();

        public gamecardScreen(Game game, string usuario) : base(game) 
        {
            _usuario = usuario;
        }

        public override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            
            // Cargar cartas desde Content
            _cartas[0] = Content.Load<Texture2D>("ace");
            _cartas[1] = Content.Load<Texture2D>("jack");
            _cartas[2] = Content.Load<Texture2D>("king"); 
            _cartas[3] = Content.Load<Texture2D>("queen");
            
            // Cargar fuente (usa el archivo .fnt que tienes)
            _fuente = Content.Load<BitmapFont>("kenney-rocket-square");
        }

        // Método para procesar el string del servidor
        private void ProcesarCartasDelServidor(string cartasString)
        {
            // Formato esperado: "cards/2/1/1/2"
            string[] partes = cartasString.Split('/');
            if (partes.Length != 5 || partes[0] != "cards")
            {
                return; // String inválido
            }

            _cartasDisponibles.Clear();

            // Procesar cada tipo de carta
            int[] cantidades = new int[4];
            for (int i = 0; i < 4; i++)
            {
                if (int.TryParse(partes[i + 1], out int cantidad))
                {
                    cantidades[i] = cantidad;
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
                 ScreenManager?.LoadScreen(new MainMenuScreen(Game, _usuario), null);
            }
            
            _estadoTecladoAnterior = estadoTeclado;
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(30, 30, 60)); // Fondo oscuro
            
            _spriteBatch.Begin();
            
            if (_mostrarTodas)
            {
                // Calcular la disposición en grid para cualquier número de cartas
                int cartasPorFila = (int)Math.Ceiling(Math.Sqrt(_cartasDisponibles.Count));
                for (int i = 0; i < _cartasDisponibles.Count; i++)
                {
                    int row = i / cartasPorFila;
                    int col = i % cartasPorFila;
                    
                    int x = col * 200 + GraphicsDevice.Viewport.Width/2 - (cartasPorFila * 100);
                    int y = row * 250 + GraphicsDevice.Viewport.Height/2 - 200;
                    
                    _spriteBatch.Draw(_cartasDisponibles[i], new Rectangle(x, y, 150, 225), Color.White);
                }
                
                // Instrucción
                string texto = "Presiona ESPACIO para seleccionar";
                var tamano = _fuente.MeasureString(texto);
                _spriteBatch.DrawString(_fuente, texto, 
                    new Vector2(GraphicsDevice.Viewport.Width/2 - tamano.Width/2, 50), 
                    Color.Gold);
            }
            else
            {
                // Dibujar carta seleccionada centrada
                _spriteBatch.Draw(_cartaSeleccionada, 
                    new Rectangle(
                        GraphicsDevice.Viewport.Width/2 - 150, 
                        GraphicsDevice.Viewport.Height/2 - 225, 
                        300, 
                        450), 
                    Color.White);
                    
                // Instrucción
                string texto = "Presiona ESPACIO para volver";
                var tamano = _fuente.MeasureString(texto);
                _spriteBatch.DrawString(_fuente, texto, 
                    new Vector2(GraphicsDevice.Viewport.Width/2 - tamano.Width/2, 50), 
                    Color.Gold);
            }
            
            _spriteBatch.End();
        }
    }
}