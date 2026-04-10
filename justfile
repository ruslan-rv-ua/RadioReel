app     := "src/RadioReel.App"
tests   := "tests/RadioReel.Tests"
rid     := "win-x64"
publish := app / "bin/Release/net10.0-windows10.0.22000.0" / rid / "publish"

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
    explorer {{publish}}

# створити ZIP-архів для поширення (без PDB)
dist: publish
    powershell -Command "\
        $src = '{{publish}}'; \
        $out = 'radioreel-dist.zip'; \
        Remove-Item $out -ErrorAction SilentlyContinue; \
        Compress-Archive -Path (Get-ChildItem $src -Exclude '*.pdb') -DestinationPath $out; \
        Write-Host \"Створено: $out ($([math]::Round((Get-Item $out).Length/1MB, 1)) MB)\""

# очистити артефакти збірки
clean:
    dotnet clean
    rm -rf src/RadioReel.App/bin src/RadioReel.App/obj
    rm -rf tests/RadioReel.Tests/bin tests/RadioReel.Tests/obj

# відновити NuGet-пакети
restore:
    dotnet restore

# збірка + тести (CI-перевірка)
ci: restore build test

# перегляд логів застосунку (останні 50 рядків)
logs:
    powershell -Command "\
        $log = Get-ChildItem logs -Filter '*.log' -ErrorAction SilentlyContinue | \
               Sort-Object LastWriteTime -Descending | Select-Object -First 1; \
        if ($log) { Get-Content $log.FullName -Tail 50 } \
        else { Write-Host 'Логів не знайдено (папка logs/ відсутня або порожня)' }"
