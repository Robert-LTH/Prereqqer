using Prereqqer.Core.Definitions;
using Avalonia.Media;
using Prereqqer.App.Resources;

namespace Prereqqer.App.ViewModels;

public static class OutcomePresentation
{
    private static readonly Geometry PassedIconGeometry = Geometry.Parse("M 10 18 A 8 8 0 1 0 10 2 A 8 8 0 1 0 10 18 M 6.5 10 L 9 12.5 L 14 7.5");

    private static readonly Geometry WarningIconGeometry = Geometry.Parse("M 10 2 L 18 17 H 2 Z M 10 7 V 11 M 10 14 L 10.01 14");

    private static readonly Geometry CancelledIconGeometry = Geometry.Parse("M 10 18 A 8 8 0 1 0 10 2 A 8 8 0 1 0 10 18 M 6.5 6.5 L 13.5 13.5 M 13.5 6.5 L 6.5 13.5");

    public static string ToDisplayName(CheckOutcome? outcome) =>
        outcome switch
        {
            CheckOutcome.Passed => Strings.OutcomePassed,
            CheckOutcome.PassedWithWarning => Strings.OutcomePassedWithWarning,
            CheckOutcome.Warning => Strings.OutcomeWarning,
            CheckOutcome.Error => Strings.OutcomeError,
            CheckOutcome.Cancelled => Strings.OutcomeCancelled,
            null => Strings.NotRun,
            _ => outcome.ToString() ?? Strings.NotRun
        };

    public static IBrush ToBackgroundBrush(CheckOutcome? outcome) =>
        Brush.Parse(outcome switch
        {
            CheckOutcome.Passed => "#DCFCE7",
            CheckOutcome.PassedWithWarning => "#FEF3C7",
            CheckOutcome.Warning => "#FFEDD5",
            CheckOutcome.Error => "#FEE2E2",
            CheckOutcome.Cancelled => "#F1F5F9",
            null => "#F8FAFC",
            _ => "#F8FAFC"
        });

    public static IBrush ToBorderBrush(CheckOutcome? outcome) =>
        Brush.Parse(outcome switch
        {
            CheckOutcome.Passed => "#86EFAC",
            CheckOutcome.PassedWithWarning => "#FCD34D",
            CheckOutcome.Warning => "#FDBA74",
            CheckOutcome.Error => "#FCA5A5",
            CheckOutcome.Cancelled => "#94A3B8",
            null => "#CBD5E1",
            _ => "#CBD5E1"
        });

    public static IBrush ToForegroundBrush(CheckOutcome? outcome) =>
        Brush.Parse(outcome switch
        {
            CheckOutcome.Passed => "#166534",
            CheckOutcome.PassedWithWarning => "#92400E",
            CheckOutcome.Warning => "#9A3412",
            CheckOutcome.Error => "#991B1B",
            CheckOutcome.Cancelled => "#475569",
            null => "#475569",
            _ => "#475569"
        });

    public static Geometry ToStatusIconGeometry(CheckOutcome? outcome) =>
        outcome switch
        {
            CheckOutcome.Cancelled => CancelledIconGeometry,
            CheckOutcome.Warning or CheckOutcome.PassedWithWarning or CheckOutcome.Error => WarningIconGeometry,
            _ => PassedIconGeometry
        };
}
