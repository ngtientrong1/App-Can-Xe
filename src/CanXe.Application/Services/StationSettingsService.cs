using CanXe.Application.Interfaces;
using CanXe.Application.Models;

namespace CanXe.Application.Services;

public sealed class StationSettingsService
{
    private readonly IStationSettingsRepository _stationRepository;
    private readonly IScaleDeviceSettingsRepository _scaleRepository;
    private readonly ICameraDeviceSettingsRepository _cameraRepository;
    private readonly ISecretProtector _secretProtector;

    public StationSettingsService(
        IStationSettingsRepository stationRepository,
        IScaleDeviceSettingsRepository scaleRepository,
        ICameraDeviceSettingsRepository cameraRepository,
        ISecretProtector secretProtector)
    {
        _stationRepository = stationRepository;
        _scaleRepository = scaleRepository;
        _cameraRepository = cameraRepository;
        _secretProtector = secretProtector;
    }

    public async Task<StationSettingsDto> GetStationAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _stationRepository.GetAsync(cancellationToken);
        return existing ?? new StationSettingsDto();
    }

    public async Task<ScaleDeviceSettingsDto> GetScaleAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _scaleRepository.GetAsync(cancellationToken);
        return existing ?? new ScaleDeviceSettingsDto();
    }

    public async Task<CameraDeviceSettingsDto> GetCameraAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _cameraRepository.GetAsync(cancellationToken);
        return existing ?? new CameraDeviceSettingsDto();
    }

    public async Task<CameraRuntimeSettings> GetCameraRuntimeAsync(CancellationToken cancellationToken = default)
    {
        var runtime = await _cameraRepository.GetRuntimeAsync(cancellationToken);
        return runtime ?? new CameraRuntimeSettings();
    }

    public async Task<(bool Success, SettingsValidationResult Validation)> SaveStationAsync(
        StationSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        var validation = SettingsValidationService.ValidateStation(dto);
        if (!validation.IsValid)
            return (false, validation);

        await _stationRepository.SaveAsync(dto, cancellationToken);
        return (true, validation);
    }

    public async Task SaveScaleAsync(ScaleDeviceSettingsDto dto, CancellationToken cancellationToken = default) =>
        await _scaleRepository.SaveAsync(dto, cancellationToken);

    public async Task<(bool Success, SettingsValidationResult Validation)> SaveCameraAsync(
        CameraDeviceSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        var validation = SettingsValidationService.ValidateCamera(dto);
        if (!validation.IsValid)
            return (false, validation);

        await _cameraRepository.SaveAsync(dto, cancellationToken);
        return (true, validation);
    }
}
