using DeskQuadra.Core.Contracts;

namespace DeskQuadra.Core.Tests;

public class InputDeviceDetectorContractTests
{
    private class FakeInputDeviceDetector : IInputDeviceDetector
    {
        private bool _isTouchActive;

        public bool IsTouchActive => _isTouchActive;

        public event EventHandler<bool>? TouchStateChanged;

        public void SetTouchActive(bool isTouch)
        {
            if (_isTouchActive != isTouch)
            {
                _isTouchActive = isTouch;
                TouchStateChanged?.Invoke(this, isTouch);
            }
        }
    }

    [Fact]
    public void InitialState_DefaultsToMouse()
    {
        var detector = new FakeInputDeviceDetector();
        Assert.False(detector.IsTouchActive);
    }

    [Fact]
    public void SetTouchActive_True_UpdatesStateAndRaisesEvent()
    {
        var detector = new FakeInputDeviceDetector();
        bool eventFired = false;
        bool? receivedState = null;

        detector.TouchStateChanged += (s, isTouch) =>
        {
            eventFired = true;
            receivedState = isTouch;
        };

        detector.SetTouchActive(true);

        Assert.True(detector.IsTouchActive);
        Assert.True(eventFired);
        Assert.True(receivedState);
    }

    [Fact]
    public void SetTouchActive_SameValue_DoesNotRaiseDuplicateEvent()
    {
        var detector = new FakeInputDeviceDetector();
        detector.SetTouchActive(true);

        int eventCount = 0;
        detector.TouchStateChanged += (s, isTouch) => eventCount++;

        detector.SetTouchActive(true);

        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void TransitionFromTouchToMouse_UpdatesCorrectly()
    {
        var detector = new FakeInputDeviceDetector();
        detector.SetTouchActive(true);
        Assert.True(detector.IsTouchActive);

        bool eventFired = false;
        detector.TouchStateChanged += (s, isTouch) =>
        {
            eventFired = true;
            Assert.False(isTouch);
        };

        detector.SetTouchActive(false);

        Assert.False(detector.IsTouchActive);
        Assert.True(eventFired);
    }
}
