# Split CharacterManager into Common / Sprite2D / Prefab3D subfolders.
# Namespaces stay unchanged (file path != namespace; pragmatic to avoid cascading using updates).
$ErrorActionPreference = 'Stop'

$root = 'Scripts/EssSystem/Core/Presentation/CharacterManager'

# Phase 1: Create new directories
foreach ($d in 'Common/Dao/Config','Common/Runtime/Preview',
              'Sprite2D/Dao','Sprite2D/Runtime','Sprite2D/Editor',
              'Prefab3D/Dao','Prefab3D/Runtime','Prefab3D/Editor') {
    New-Item -ItemType Directory -Force -Path "$root/$d" | Out-Null
}

# Phase 2: git mv files (with .meta sibling)
function GitMove($src, $dst) {
    Write-Host "git mv $src -> $dst"
    git mv -- "$root/$src" "$root/$dst"
    git mv -- "$root/$src.meta" "$root/$dst.meta"
}

# ─── Common ──────────────────────────────────────────
GitMove 'Dao/Character.cs' 'Common/Dao/Character.cs'
foreach ($f in 'CharacterActionConfig','CharacterConfig','CharacterLocomotionRole',
               'CharacterPartConfig','CharacterPartType','CharacterRenderMode') {
    GitMove "Dao/Config/$f.cs" "Common/Dao/Config/$f.cs"
}
GitMove 'Runtime/CharacterPartView.cs' 'Common/Runtime/CharacterPartView.cs'
GitMove 'Runtime/CharacterView.cs'     'Common/Runtime/CharacterView.cs'
GitMove 'Runtime/Preview/CharacterPreviewPanel.cs' 'Common/Runtime/Preview/CharacterPreviewPanel.cs'

# ─── Sprite2D ────────────────────────────────────────
GitMove 'Dao/CharacterVariantPools.cs'      'Sprite2D/Dao/CharacterVariantPools.cs'
GitMove 'Dao/DefaultCharacterConfigs.cs'    'Sprite2D/Dao/DefaultCharacterConfigs.cs'
GitMove 'Dao/DefaultTreeCharacterConfigs.cs' 'Sprite2D/Dao/DefaultTreeCharacterConfigs.cs'
GitMove 'Runtime/CharacterPartView2D.cs'         'Sprite2D/Runtime/CharacterPartView2D.cs'
GitMove 'Runtime/CharacterPartView2DAnimator.cs' 'Sprite2D/Runtime/CharacterPartView2DAnimator.cs'
GitMove 'Editor/CharacterAnimatorBaseControllerBuilder.cs' 'Sprite2D/Editor/CharacterAnimatorBaseControllerBuilder.cs'
GitMove 'Editor/CharacterSpriteSheetSlicer.cs' 'Sprite2D/Editor/CharacterSpriteSheetSlicer.cs'

# ─── Prefab3D ────────────────────────────────────────
GitMove 'Runtime/CharacterPartView3D.cs'      'Prefab3D/Runtime/CharacterPartView3D.cs'
GitMove 'Runtime/CharacterPartView3DClips.cs' 'Prefab3D/Runtime/CharacterPartView3DClips.cs'
GitMove 'Runtime/CharacterAnimatorBinder.cs'  'Prefab3D/Runtime/CharacterAnimatorBinder.cs'
GitMove 'Editor/FBXAnimatorControllerBuilder.cs' 'Prefab3D/Editor/FBXAnimatorControllerBuilder.cs'
GitMove 'Editor/FBXManifestBuilder.cs'           'Prefab3D/Editor/FBXManifestBuilder.cs'

Write-Host ""
Write-Host "Done. CharacterConfigFactory.cs left in Dao/ for manual split (next step)."
