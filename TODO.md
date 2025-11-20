# TODO LIST

- Children are pests - they will make all kind of problems in the colony because kids are stupid

1. Misbehavior Factor Calculation
  Based on parental opinion and guidance:
   - Direct parental relationship opinion (higher opinion = lower misbehavior factor)
   - Time since last meaningful interaction with parents/guardians
   - Number of available caregivers (0 = maximum misbehavior factor)
   - Child's specific traits that affect behavior (rebellious, impulsive, etc.)
   - run away in fear when taking damage and cry at one of the parents if found in rage

2. Misbehavior Types Implementation

  Level 1 - Annoying Adults (Low Severity):
   - Approach adults during work and ask "annoying" questions (triggers negative mood) - done
   - If a child gets insulted there's a chance they go to their parent (or most liked pawn if no parents) and cry/bawl about it. get insult subject and who did it from play log and put it in subject field. - done
   - taking damage will make the child flee in terror and cry, if parent close they go and whine about it to parent and annoy them. makes child soldiers unreliable (chance of this happening depending on their shooting/melee skill, very low once its past 10) - done
        - Fix negative mood thought not being applied to parent if they failed to console child

  Level 2 - Item Misplacement (Moderate Severity):
   - Taking valuable items from storage and playing with them, high chance of damaging the item in the process. leaves item in the field once they are done. - done
   - Making bad food (like mud balls) and placing it in storage.
   - Spy on couples during intimate moments (possibly disrupting the encounter and/or getting a beating from one of them) - done

  Level 3 - Property Damage (High Severity):
   - Damaging crops by trampling them during play
   - Breaking weapons/apparel stored inappropriately accessible to children
   - Deconstructing furniture/buildings (if child can reach)

  Level 4 - Dangerous Behavior (Critical Severity):
   - Attempting to light fires in inappropriate places
   - Attempting to use equipped weapons on random animals or people
   - Leak location of colony to raiders when using radio
   - Help prisoners escape
   - Run away from home (become global pawn)

