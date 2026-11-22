using Tweaker.Core.Models;

namespace Tweaker.Core.Services;

public interface ITweakEngine
{
    TweakState GetState(Tweak tweak);
    void Apply(Tweak tweak);
    void Revert(Tweak tweak);
}
