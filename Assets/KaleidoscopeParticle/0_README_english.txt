KaleidoscopeParticle (Unity package)
Created by Oyakawa Daiki



////// Feature of this package //////
This asset can make various Kaleidoscope effect.
Effect although based on a ParticleSystem, movement of each of Particle another script is controlling.
KaleidoscopeParticle is 3D particle effect. You can use as magic, explosion and background.
Making the pattern of Kaleidoscope is easy.
Setting the position and angle and size of the one part, and it is copied automatically become a beautiful pattern.

To actually see, please open "/KaleidoscopeParticle/Demo/SimpleEffect.unity".



////// How to use //////
1. Create KaleidoscopeParticle
  Please open the "KaleidoscopeParticle/Demo/Simple_Effect.unity".
  (or use "KaleidoscopeParticle/Prefab/Kaleidoscope_Simple.prefab" for your scene.)

2. Play Scene and move the "Target"
  Please play the scene. 4 particle is created.
  Please move the "Kaleidoscope_Simple/Ring(1)/Target". (position, rotation, scale)
  When you move the "Target", all of the particle moving!

3. Change the setting of KaleidoscopeParticle
  Stop the scene.
  Take a look at Kaleidoscope component in object "Ring(1)".
   - Base Object : This is the object "Target"
   - Circle Value : The number of particle.
   - X Mirror : When true, particle will be symmetrical.
   - Color : This will change the color of the particle.

  Let's change to "CircleValue=8, XMirror=true". And play the scene.
  It has become more complex pattern!!



////// Other //////
- I want to increase the circle.
  Copy the object "Ring(1)".

- I do not want to loop animation.
  Take a look at ParticleSystem component in object "Ring(1)".
  Please change the Looping to false. Duration is the entire time.
  And, the final alpha value of Color Please change to 0(Kaleidoscope component).

- I want to move the pattern in real time.
  Pattern will change if moving the object "Target" in the script.
  Please refer to the scene "KaleidoscopeParticle/Demo/Texture_Effect.unity".



////// Contact information //////
Oyakawa Daiki (Japan)

Email : daiki.evilone@gmail.com
Twitter : https://twitter.com/daiki_all
FaceBook : https://www.facebook.com/OyakawaDaiki