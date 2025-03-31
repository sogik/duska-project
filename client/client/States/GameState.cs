using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Net;

namespace client
{
  public class GameState : State
  {
    private List<Component> _components;
    private NetworkClient _networkClient;
    private SpriteFont _font;
    private string _statusMessage = "Connecting...";
    private string _serverMessage = ""

    public GameState(Game1 game, GraphicsDevice graphicsDevice, ContentManager content)
      : base(game, graphicsDevice, content)
    {
      _networkClient = new NetworkClient("192.168.56.102", 9050);
      _networkClient.OnConnected += OnConnected;
      _networkClient.OnDisconnected += OnDisconnected;
      _networkClient.OnMessageReceived += OnMessageReceived;
      _networkClient.Connect();

      var buttonTexture = _content.Load<Texture2D>("Textures/button");
      var buttonFont = _content.Load<SpriteFont>("Fonts/arial");

      var newGameButton = new Button(buttonTexture, buttonFont)
      {
        Position = new Vector2(300, 200),
        Text = "New sdddd",
      };

      newGameButton.Click += NewGameButton_Click;

      var loadGameButton = new Button(buttonTexture, buttonFont)
      {
        Position = new Vector2(300, 250),
        Text = "Load Game",
      };

      loadGameButton.Click += LoadGameButton_Click;

      var quitGameButton = new Button(buttonTexture, buttonFont)
      {
        Position = new Vector2(300, 300),
        Text = "Quit Game",
      };

      quitGameButton.Click += QuitGameButton_Click;

      _components = new List<Component>()
      {
        newGameButton,
        loadGameButton,
        quitGameButton,
      };
    }
    private void LoadGameButton_Click(object sender, EventArgs e)
    {
      Console.WriteLine("Load Game");
    }

    private void NewGameButton_Click(object sender, EventArgs e)
    {
      _game.ChangeState(new GameState(_game, _graphicsDevice, _content));
    }

    private void QuitGameButton_Click(object sender, EventArgs e)
    {
      _game.Exit();
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
      spriteBatch.Begin();

      foreach (var component in _components)
        component.Draw(gameTime, spriteBatch);

      spriteBatch.End();

    }

    public override void PostUpdate(GameTime gameTime)
    {

    }

    public override void Update(GameTime gameTime)
    {
      foreach (var component in _components)
        component.Update(gameTime);
    }
  }
}
