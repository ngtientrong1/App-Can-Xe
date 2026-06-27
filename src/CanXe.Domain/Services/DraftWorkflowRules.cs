namespace CanXe.Domain.Services;

public static class DraftWorkflowRules
{
    public static bool CanUpdateWeight1(bool isWeight1LockedFromSaved, bool hasDraftWeight2, bool developerWeight1OverrideEnabled) =>
        !isWeight1LockedFromSaved && (!hasDraftWeight2 || developerWeight1OverrideEnabled);

    public static bool CanUpdateWeight2(bool isWeight2LockedFromSaved) =>
        !isWeight2LockedFromSaved;
}
