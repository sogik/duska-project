using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace client
{
    public class OptionsState : State
    {
        private List<Component> _components;

        public OptionsState(Game1 game, GraphicsDevice graphicsDevice, ContentManager content)
          : base(game, graphicsDevice, content)
        {
            var buttonTexture = _content.Load<Texture2D>("Textures/button");
            var buttonFont = _content.Load<SpriteFont>("Fonts/arial");

            var quitGameButton = new Button(buttonTexture, buttonFont)
            {
                Position = new Vector2(300, 200),
                Text = "Quit Game",
            };

            quitGameButton.Click += QuitGameButton_Click;

            _components = new List<Component>()
            {
                quitGameButton,
            };
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
