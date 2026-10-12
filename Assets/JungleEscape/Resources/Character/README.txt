PLAYER CHARACTER (skinned model + motion-capture animations)
==========================================================

Drop FBX files in THIS folder with exactly these names. They are set up
automatically on import (Humanoid rig, looping clips, no drifting).

  Character.fbx   the rigged character model                     (required)
  Run.fbx         running loop                                    (required)
  Jump.fbx        jump: take-off, air, landing                    (recommended)
  Roll.fbx        forward roll / slide under obstacles            (recommended)
  Idle.fbx        standing idle, shown on the menu                (recommended)
  Fall.fbx        falling backwards when caught                   (optional)
  Stumble.fbx     short stagger / hit reaction                    (optional)
  Glide.fbx       hanging / falling idle, used with Leaf Glider   (optional)

If Character.fbx or Run.fbx is missing, the built-in procedural boy is used.

GETTING THEM FREE FROM MIXAMO (https://www.mixamo.com, free Adobe account)
--------------------------------------------------------------------------
1. Character: pick a character on the Characters tab, OR upload your own
   jungle-boy model (FBX/OBJ) and let Mixamo auto-rig it.
   Download:  Format = FBX for Unity, Pose = T-pose.  Save as Character.fbx

2. Animations (with that character selected), search and download each with
   Format = FBX for Unity, Skin = Without Skin, Frames = 30,
   and TICK "In Place" whenever that checkbox is offered:
     "Running"                 -> Run.fbx
     "Jump" or "Running Jump"  -> Jump.fbx
     "Running Slide" or "Sprinting Forward Roll" -> Roll.fbx
     "Breathing Idle"          -> Idle.fbx
     "Falling Back Death"      -> Fall.fbx
     "Stumble Backwards" or "Hit Reaction" -> Stumble.fbx
     "Falling Idle" or "Hanging Idle" -> Glide.fbx

3. Copy the files here and press Play.
