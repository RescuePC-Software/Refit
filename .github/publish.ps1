$csproj = Join-Path $PSScriptRoot "..\Source\RescuePC.Software.Refit\RescuePC.Software.Refit.csproj"
$xml = [xml](Get-Content $csproj)
$version = $xml.Project.PropertyGroup.Version

if (-not $version) {
	Write-Error "Nie znaleziono elementu <Version> w pliku $csproj."
	exit 1
}

Write-Host "Wersja odczytana z csproj: $version" -ForegroundColor DarkCyan
$tag = "v$version"

$status = git status --porcelain
if ($status) {
    Write-Error "Są niezacommitowane zmiany. Zrób commit przed publikacją."
    exit 1
}

Write-Host "Wypychanie brancha..." -ForegroundColor Cyan
git push origin
if ($LASTEXITCODE -ne 0) {
    Write-Error "Nie udało się wypchnąć brancha."
    exit 1
}

Write-Host "Tworzenie taga $tag (wersja z csproj: $version)..." -ForegroundColor Cyan

git tag $tag
if ($LASTEXITCODE -ne 0) {
	Write-Error "Nie udało się utworzyć taga $tag."
	exit 1
}

git push origin $tag
if ($LASTEXITCODE -ne 0) {
	Write-Error "Nie udało się wypchnąć taga $tag."
	exit 1
}

Write-Host "Tag $tag został wypchnięty. GitHub Actions opublikuje paczkę NuGet." -ForegroundColor Green