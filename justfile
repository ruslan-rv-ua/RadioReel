set shell := ["powershell.exe", "-NoLogo", "-Command"]

app     := "src/RadioReel.App"
tests   := "tests/RadioReel.Tests"
rid     := "win-x64"
publish := app + "/bin/Release/net10.0-windows10.0.22000.0/" + rid + "/publish"
dist    := "radioreel-dist.zip"

# показати список рецептів
default:
    @just --list

# зібрати проект (Debug)
build:
    dotnet build

# запустити застосунок
run:
    dotnet run --project {{app}}

# запустити тести
test:
    dotnet test

# запустити тести з детальним виводом
test-verbose:
    dotnet test --logger "console;verbosity=detailed"

# зібрати Release EXE для поширення
publish:
    dotnet publish {{app}} -c Release -r {{rid}}

# відкрити папку з релізом у провіднику
open-publish: publish
    Start-Process explorer "{{publish}}"

# створити ZIP-архів для поширення (без PDB)
dist: publish
    if (Test-Path "{{dist}}") { Remove-Item "{{dist}}" }
    Get-ChildItem "{{publish}}" -Exclude "*.pdb" | Compress-Archive -DestinationPath "{{dist}}"
    Write-Host "Створено: {{dist}} ($([math]::Round((Get-Item '{{dist}}').Length / 1MB, 1)) MB)"

# очистити артефакти збірки
clean:
    dotnet clean
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "{{app}}/bin", "{{app}}/obj", "{{tests}}/bin", "{{tests}}/obj"

# відновити NuGet-пакети
restore:
    dotnet restore

# збірка + тести (CI-перевірка)
ci: restore build test

# перегляд логів застосунку (останні 50 рядків)
logs:
    $log = Get-ChildItem logs -Filter "*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if ($log) { Get-Content $log.FullName -Tail 50 } else { Write-Host "Логів не знайдено" }
