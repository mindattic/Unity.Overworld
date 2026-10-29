using MindAttic.Overworld.Runtime;
using NUnit.Framework;
using Unity.Shared.Core;
using UnityEngine;

namespace MindAttic.Overworld.Tests
{
    // The CLI mute (-muted / MINDATTIC_CLI_MUTED=1) reaches Overworld through Unity.Shared.Core.Profiles.SessionMuted;
    // SessionAudio turns that into a silent listener for the run, without touching a profile or settings.json.
    public class SessionAudioTests
    {
        private bool priorMuted;
        private float priorVolume;

        [SetUp]
        public void SetUp()
        {
            priorMuted = Profiles.SessionMuted;
            priorVolume = AudioListener.volume;
            AudioListener.volume = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Profiles.SessionMuted = priorMuted;
            AudioListener.volume = priorVolume;
        }

        [Test]
        public void SessionMutedSilencesTheListener()
        {
            Profiles.SessionMuted = true;

            Assert.That(SessionAudio.Apply(), Is.True);
            Assert.That(SessionAudio.Muted, Is.True);
            Assert.That(AudioListener.volume, Is.EqualTo(0f));
        }

        [Test]
        public void UnmutedSessionLeavesTheListenerAlone()
        {
            Profiles.SessionMuted = false;

            Assert.That(SessionAudio.Apply(), Is.False);
            Assert.That(AudioListener.volume, Is.EqualTo(1f));
        }
    }
}
