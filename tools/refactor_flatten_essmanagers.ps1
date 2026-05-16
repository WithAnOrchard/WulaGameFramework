# Flatten: pull Foundation / Presentation / Gameplay out of EssManagers/, make siblings of Base/.
# Then update 3 namespace prefixes. Run from Assets/ (git root).
$ErrorActionPreference = 'Stop'

$root = 'Scripts/EssSystem/Core'

function GitMv($src, $dst) {
    Write-Host "git mv $src -> $dst"
    git mv -- $src $dst
}

# 1) Move 3 layers up to Core/.
foreach ($layer in 'Foundation','Presentation','Gameplay') {
    GitMv "$root/EssManagers/$layer"      "$root/$layer"
    GitMv "$root/EssManagers/$layer.meta" "$root/$layer.meta"
}

# 2) Move the managers overview doc out of EssManagers/.
GitMv "$root/EssManagers/Agent.md"      "$root/Managers.md"
GitMv "$root/EssManagers/Agent.md.meta" "$root/Managers.md.meta"

# 3) Remove the now-empty EssManagers folder + its sibling .meta.
git rm -- "$root/EssManagers.meta"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$root/EssManagers"

Write-Host ""
Write-Host "Phase A done. EssManagers/ flattened. Now run namespace replacement."
