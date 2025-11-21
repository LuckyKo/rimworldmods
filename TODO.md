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
   - Doorway Loitering: Two children play in a doorway, holding it open and letting temperature escape.

  Level 2 - Item Misplacement (Moderate Severity):
   - Taking valuable items from storage and playing with them, high chance of damaging the item in the process. leaves item in the field once they are done. - done
   - Making bad food (like mud balls) and placing it in storage.
   - Spy on couples during intimate moments (possibly disrupting the encounter and/or getting a beating from one of them) - done
   - Playing tag with another child (disregarding allowed zones): one child talks to another to play tag together. once child A convinces child B, child A will run to a random point in a 50 cell radius, stop for a bit then run to another point. loop that for a few times. child B follows A around. - done

  Level 3 - Property Damage (High Severity):
   - Damaging crops by trampling them during play - done
   - Breaking workbenches or other furniture (not damage, but puting them in a broken state that will need repairing with components)
   - Talk to dangerous prisoners. chance to get fooled and drop all gear (weapon and apparel), child flees then runs to cry about it to parent (tweak cry job as the taking damage to reuse it here). if they get fooled there's a chance to trigger prisoner escape event from base game.

  Level 4 - Dangerous Behavior (Critical Severity):
   - Attempting to light fires in inappropriate places - done
   - Attempting to use equipped weapons on random animals or people - done
   - Leak location of colony to raiders when using radio. trigger a raid event one day later.
   - Run away from home (become global pawn. similar to giveUp but a job instead of mental state so the player can interupt it easily)

