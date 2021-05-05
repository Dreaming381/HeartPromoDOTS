# Heart Promo DOTS

This repo contains the source code and assets used for a workshop for teaching
the Unity’s C\# Job System and Burst for GameObject-based projects.

This workshop was originally a 6 hour workshop presented on Valentine’s Day of
2020, but has since been revamped for use in 2020.3 LTS and the latest 3D Game
Kit. At the time of writing, a variation of this new version was presented(link
this word) by Roger Kueng.

## Usage

This demo is often presented using Unity’s 3D Game Kit, but will likely work in
any project that uses the built-in render pipeline.

The root directory contains a separate HeartsPromoManagerX.cs for each
iteration. Simply drag this onto an empty GameObject in the scene and populate
the fields. Then press play and watch the magic happen.

## HeartsCleanup

This directory contains a more advanced implementation using a ScriptableObject
architecture for more modular control over the hearts. Add the HeartsManager.cs
script to an empty GameObject. Then populate the array with the included
Scriptable Object instances. Feel free to experiment with the order, create new
instances, or even add new processors.

## Can I use this in production?

Please note that this demo has chosen clarity over optimization in several
areas. But in spite of that, it is already capable of doing things traditionally
not possible with Unity. Feel free to adapt it to your use case.
