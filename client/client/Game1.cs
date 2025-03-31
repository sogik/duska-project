using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace client
{
  public class Game1 : Game
  {
    GraphicsDeviceManager graphics;
    SpriteBatch spriteBatch;

    private State _currentState;

    private State _nextState;

    public NetworkManager NetworkManager { get; private set; }

    public void ChangeState(State state)
    {
      _nextState = state;
    }

    public Game1()
    {
      graphics = new GraphicsDeviceManager(this);
      Content.RootDirectory = "Content";

      NetworkManager = new NetworkManager(this, "192.168.56.102", 9050);
    }
    protected override void Initialize()
    {
      IsMouseVisible = true;

      base.Initialize();
    }

    protected override void LoadContent()
    {
      // Create a new SpriteBatch, which can be used to draw textures.
      spriteBatch = new SpriteBatch(GraphicsDevice);

      _currentState = new MenuState(this, graphics.GraphicsDevice, Content);
    }

    protected override void UnloadContent()
    {
      // TODO: Unload any non ContentManager content here
    }

    protected override void Update(GameTime gameTime)
    {
      if (_nextState != null)
      {
        _currentState = _nextState;

        _nextState = null;
      }

      _currentState.Update(gameTime);

      _currentState.PostUpdate(gameTime);

      base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
      GraphicsDevice.Clear(Color.Black);

      _currentState.Draw(gameTime, spriteBatch);

      base.Draw(gameTime);
    }
  }
}
