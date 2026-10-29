using MindAttic.Overworld.Core;
using MindAttic.Overworld.Runtime;
using NUnit.Framework;

namespace MindAttic.Overworld.Tests
{
    public class FoundationTests
    {
        [Test]
        public void GameHostCarriesTheGame()
        {
            var game = new Game();
            var host = GameHost.Create(game);
            try { Assert.AreSame(game, host.Game); }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }
    }
}
