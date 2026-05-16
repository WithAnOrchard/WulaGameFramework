# Phase A: move 7 Manager modules into new layered layout.
# Run from Assets/ (git root). Uses git mv to preserve history.
$ErrorActionPreference = 'Stop'

$root = 'Scripts/EssSystem/Core/EssManagers'

function GitMv($src, $dst) {
    Write-Host "git mv $src -> $dst"
    git mv -- $src $dst
}

# 1) CharacterManager: Gameplay -> Presentation
GitMv "$root/Gameplay/CharacterManager"      "$root/Presentation/CharacterManager"
GitMv "$root/Gameplay/CharacterManager.meta" "$root/Presentation/CharacterManager.meta"

# 2) create Gameplay/World and Gameplay/Systems
New-Item -ItemType Directory -Force -Path "$root/Gameplay/World"   | Out-Null
New-Item -ItemType Directory -Force -Path "$root/Gameplay/Systems" | Out-Null

# 3) move into World/
foreach ($name in 'MapManager','EntityManager','BuildingManager') {
    GitMv "$root/Gameplay/$name"      "$root/Gameplay/World/$name"
    GitMv "$root/Gameplay/$name.meta" "$root/Gameplay/World/$name.meta"
}

# 4) move into Systems/
foreach ($name in 'InventoryManager','DialogueManager','SkillManager') {
    GitMv "$root/Gameplay/$name"      "$root/Gameplay/Systems/$name"
    GitMv "$root/Gameplay/$name.meta" "$root/Gameplay/Systems/$name.meta"
}

Write-Host "Phase A done. Unity will auto-generate .meta for Gameplay/World and Gameplay/Systems on next refresh."
