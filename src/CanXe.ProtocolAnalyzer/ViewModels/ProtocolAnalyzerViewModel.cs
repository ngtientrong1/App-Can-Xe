using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CanXe.ProtocolAnalyzer.Core;
using CanXe.ProtocolAnalyzer.Core.Analysis;
using CanXe.ProtocolAnalyzer.Core.Models;
using CanXe.ProtocolAnalyzer.Core.Parsing;
using CanXe.ProtocolAnalyzer.Core.Reporting;
using CanXe.ProtocolAnalyzer.Core.Reconstruction;
using CanXe.ProtocolAnalyzer.Core.Replay;
using Microsoft.Win32;

namespace CanXe.ProtocolAnalyzer.ViewModels;

public sealed class ProtocolAnalyzerViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISerialLogParser _parser = new SerialLogParser();
    private readonly ISerialStreamReconstructor _reconstructor = new SerialStreamReconstructor();
    private readonly ProtocolAnalysisOrchestrator _orchestrator = new();
    private readonly ProtocolAnalysisReportWriter _reportWriter = new();
    private LogReplayEngine? _replay;
    private SessionRowViewModel? _selectedRow;
    private SerialLogSession? _selectedSession;
    private ProtocolAnalysisResult? _lastResult;
    private string _statusText = "Sẵn sàng import log offline.";
    private string _replayHex = string.Empty;
    private string _accumulatedHex = string.Empty;
    private string _analysisOutput = string.Empty;
    private double _replaySpeed = 1.0;

    public ProtocolAnalyzerViewModel()
    {
        ImportFilesCommand = new RelayCommand(ImportFiles);
        RunFullAnalysisCommand = new RelayCommand(RunFullAnalysis, () => Sessions.Count > 0);
        ExportReportCommand = new RelayCommand(async () => await ExportReportAsync(), () => _lastResult is not null);
        PlayCommand = new RelayCommand(async () => await PlayReplayAsync(false));
        PauseCommand = new RelayCommand(() => _replay?.Pause());
        StopCommand = new RelayCommand(() => _replay?.Stop());
        InstantCommand = new RelayCommand(async () => await PlayReplayAsync(true));
    }

    public ObservableCollection<SessionRowViewModel> Sessions { get; } = [];
    public ObservableCollection<CandidateRowViewModel> Candidates { get; } = [];

    public SessionRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!SetField(ref _selectedRow, value))
                return;
            SelectedSession = value?.Session;
        }
    }

    public SerialLogSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (!SetField(ref _selectedSession, value))
                return;
            _replay?.Dispose();
            _replay = value?.Stream is null ? null : new LogReplayEngine(value);
            if (_replay is not null)
                _replay.StateChanged += OnReplayStateChanged;
        }
    }

    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
    public string ReplayHex { get => _replayHex; private set => SetField(ref _replayHex, value); }
    public string AccumulatedHex { get => _accumulatedHex; private set => SetField(ref _accumulatedHex, value); }
    public string AnalysisOutput { get => _analysisOutput; private set => SetField(ref _analysisOutput, value); }

    public double ReplaySpeed
    {
        get => _replaySpeed;
        set
        {
            if (SetField(ref _replaySpeed, value) && _replay is not null)
                _replay.SpeedMultiplier = value;
        }
    }

    public ICommand ImportFilesCommand { get; }
    public ICommand RunFullAnalysisCommand { get; }
    public ICommand ExportReportCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand InstantCommand { get; }

    public void ImportDroppedFiles(IEnumerable<string> paths) => ImportPaths(paths);

    private void ImportFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "DeviceTester log|*.log|All files|*.*"
        };
        if (dialog.ShowDialog() == true)
            ImportPaths(dialog.FileNames);
    }

    private void ImportPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(File.Exists))
        {
            if (Sessions.Any(s => s.SourcePath == path))
                continue;

            var session = _parser.ParseFile(path);
            session.Stream = _reconstructor.Reconstruct(session);
            session.Timing = new TimingAnalyzer().Analyze(session);
            Sessions.Add(new SessionRowViewModel(session));
        }

        if (Sessions.Count > 0)
            SelectedSession = Sessions[0].Session;
        StatusText = $"Đã import {Sessions.Count} phiên.";
        (RunFullAnalysisCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void RunFullAnalysis()
    {
        var paths = Sessions.Select(s => s.SourcePath).ToList();
        _lastResult = _orchestrator.Analyze(paths);
        Candidates.Clear();
        foreach (var c in _lastResult.DecoderCandidates.Take(30))
            Candidates.Add(new CandidateRowViewModel(c));

        AnalysisOutput =
            $"Timing + periodicity + bit-cell + decoder hoàn tất.{Environment.NewLine}" +
            $"Top: {_lastResult.TopCandidate?.DecoderName ?? "—"} score={_lastResult.TopCandidate?.Score:F1} confidence={_lastResult.TopCandidate?.Confidence:F1}";
        StatusText = "Phân tích xong.";
        (ExportReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private async Task ExportReportAsync()
    {
        if (_lastResult is null)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown|*.md",
            FileName = "ProtocolAnalysisReport.md"
        };
        if (dialog.ShowDialog() != true)
            return;

        var md = dialog.FileName;
        var json = Path.ChangeExtension(md, ".json");
        await _reportWriter.WriteMarkdownAsync(_lastResult, md);
        await _reportWriter.WriteJsonAsync(_lastResult, json);
        StatusText = $"Đã xuất {md} và {json}";
    }

    private async Task PlayReplayAsync(bool instant)
    {
        if (_replay is null)
            return;
        _replay.SpeedMultiplier = ReplaySpeed;
        await Task.Run(() => _replay.PlayAsync(instant)).ConfigureAwait(true);
    }

    private void OnReplayStateChanged(object? sender, LogReplayState e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ReplayHex = e.CurrentHex;
            AccumulatedHex = e.AccumulatedHex;
            StatusText = $"Replay chunk {e.ChunkNumber} @ {e.Timestamp:HH:mm:ss.fff}";
        });
    }

    public void Dispose()
    {
        _replay?.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

public sealed class SessionRowViewModel
{
    public SessionRowViewModel(SerialLogSession session)
    {
        Session = session;
        SourcePath = session.SourceFilePath;
        FileName = session.FileName;
        SessionLabel = session.Metadata.SessionLabel ?? string.Empty;
        KnownWeightKg = session.WeightLabel.KnownWeightKg;
        IsStableSession = session.WeightLabel.IsStableSession;
        ChunkCount = session.Chunks.Count;
        TotalBytes = session.Timing?.TotalBytes ?? session.Chunks.Sum(c => c.Bytes.Length);
        Duration = session.Timing?.TotalDuration ?? TimeSpan.Zero;
    }

    public SerialLogSession Session { get; }
    public string SourcePath { get; }
    public string FileName { get; }
    public string SessionLabel { get; set; }
    public decimal? KnownWeightKg { get; set; }
    public bool IsStableSession { get; set; }
    public int ChunkCount { get; }
    public int TotalBytes { get; }
    public TimeSpan Duration { get; }
}

public sealed class CandidateRowViewModel
{
    public CandidateRowViewModel(DecodedCandidate candidate)
    {
        Name = candidate.DecoderName;
        Transform = candidate.Transform.Name;
        Score = candidate.Score;
        Confidence = candidate.Confidence;
        ZeroKg = Find(candidate, "145439");
        FiftyKg = Find(candidate, "145617");
        FiveTon = Find(candidate, "145814");
        Dynamic = Find(candidate, "145841");
    }

    public string Name { get; }
    public string Transform { get; }
    public double Score { get; }
    public double Confidence { get; }
    public string ZeroKg { get; }
    public string FiftyKg { get; }
    public string FiveTon { get; }
    public string Dynamic { get; }

    private static string Find(DecodedCandidate c, string key) =>
        c.DecodedValuesBySession.FirstOrDefault(kv => kv.Key.Contains(key, StringComparison.Ordinal)).Value?.ToString("F1") ?? "—";
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
