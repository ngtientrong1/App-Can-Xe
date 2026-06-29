# FFmpeg bundle for CanXe (Windows x64)

CanXe uses **FFmpeg** as an external decoder subprocess for RTSP camera streams.

## License

FFmpeg is licensed under **LGPL v2.1+** (and some optional components under GPL). CanXe invokes `ffmpeg.exe` as a separate process and does not link FFmpeg libraries into the application binary.

Download builds from an official source such as [https://www.gyan.dev/ffmpeg/builds/](https://www.gyan.dev/ffmpeg/builds/) (release full build).

Place files here before publish:

```text
third-party/ffmpeg/win-x64/ffmpeg.exe
third-party/ffmpeg/win-x64/ffprobe.exe
third-party/ffmpeg/win-x64/LICENSE.txt
```

Publish script copies this folder to:

```text
publish/win10-x64/ffmpeg/
```

## Fallback

If bundled FFmpeg is missing, CanXe tries `ffmpeg.exe` on the system `PATH`.
