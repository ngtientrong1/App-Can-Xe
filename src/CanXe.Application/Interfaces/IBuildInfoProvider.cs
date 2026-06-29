using CanXe.Application.Models;

namespace CanXe.Application.Interfaces;

public interface IBuildInfoProvider
{
    BuildInfo GetBuildInfo();
}
