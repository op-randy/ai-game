Rebuild order (Blender 5.2, run from /home/randy/Development/games/ai-game/ArtSource/CatGirl):
 1. seg_fix.py      Tripo segmented FBX -> fixed rig + T-pose (catgirl_seg_tpose.blend)
 2. (v2 = head pieces made rigid, textures packed)
 3. mirror_leg.py   v2 -> right leg replaced by mirrored left leg
 4. mirror_head.py  -> mask (21) + head cap (23) rebuilt from their right halves
 5. bodysuit.py     -> fitted black fabric under the armor (torso, neck, arms, legs) = catgirl_seg_tpose_v6.blend
 6. unity_export.py v6 -> ../../Assets/Characters/CatGirl/CatGirl.fbx
 7. tripo_export.py v6 -> export_for_tripo/CatGirl_Tpose.fbx/.glb (no rig, one mesh)
Each script: blender -b --python <script> -- <input.blend> <output...>
