using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace client
{
  public class MenuState : State
  {
    private List<Component> _components;

    public MenuState(Game1 game, GraphicsDevice graphicsDevice, ContentManager content)
      : base(game, graphicsDevice, content)
    {
      var buttonTexture = _content.Load<Texture2D>("Textures/button");
      var buttonFont = _content.Load<SpriteFont>("Fonts/arial");

      var newGameButton = new Button(buttonTexture, buttonFont)
      {
        Position = new Vector2(300, 200),
        Text = "Start",
      };

      newGameButton.Click += NewGameButton_Click;

      var optionsButton = new Button(buttonTexture, buttonFont)
      {
        Position = new Vector2(300, 250),
        Text = "Options",
      };

      optionsButton.Click += optionsButton_Click;

      var quitGameButton = new Button(buttonTexture, buttonFont)
      {
        Position = new Vector2(300, 300),
        Text = "Quit Game",
      };

      quitGameButton.Click += QuitGameButton_Click;

      _components = new List<Component>()
      {
        newGameButton,
        optionsButton,
        quitGameButton,
      };
    }

    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
      spriteBatch.Begin();

      foreach (var component in _components)
        component.Draw(gameTime, spriteBatch);

      spriteBatch.End();
    }

    private void optionsButton_Click(object sender, EventArgs e)
    {
      _game.ChangeState(new OptionsState(_game, _graphicsDevice, _content));
    }

    private void NewGameButton_Click(object sender, EventArgs e)
    {
      _game.ChangeState(new GameState(_game, _graphicsDevice, _content));
    }

    public override void PostUpdate(GameTime gameTime)
    {
      // remove sprites if they're not needed
    }

    public override void Update(GameTime gameTime)
    {
      foreach (var component in _components)
        component.Update(gameTime);
    }

    private void QuitGameButton_Click(object sender, EventArgs e)
    {
      _game.Exit();
    }
  }
}