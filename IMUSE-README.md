# About

This library contains an interactive music system designed to be compatible with iMUSE MIDI files. (Trademark used nominatively)

Implementation is based on the (now expired) patent [US5315057)](https://patents.google.com/patent/US5315057).

`iMUSE` is a trademark for an implemention of the system described by the above patent.  This library is designed to be able to play similarly composed (binary compatible) MIDI sequence files.  In this project, `iMUSE file` refers to any MIDI sequence designed to be played in such a system (e.g., Type 2 MIDI file containing any part/looping markers that would be interpreted by such a system.)

# Quick Start - using the debug panel

The debug panel is a manual interface for controlling the default IMuseDirector class.  This is useful for testing purproses such as when composing a new interactive music file.

- Open this project in Unity
- As the panel uses TextMesh Pro, TMP resources must be imported into the project.  Go to: `Window -> TextMesh Pro -> Import TMP Essential Resources`
- Open scene `Assets/UnityMidiExamples/IMuse/IMuseTest.unity`
- Look for the IMuseDebugConsoleCanvas, in the DebugIMuseConsole component, set in midiFileDirectory the full path to a directory containing midi files.
- Enter play mode
- Select a MIDI file from the dropdown and click Play.  (All files in the directory will be listed)

If you don't have an example dynamic midi file, consider using an LFD archiving tool to extract one from a game.  X-Wing's MMMUSIC.LFD/halmarch.GMID file is a good example of various flow control and part features. Channels 3, 5, and 6 are examples of parts that are enabled when a door is moused over.  HookJump 0 makes the main theme loop indefinitely. HookJumps 1 & 2 are examples of dynamic transitions to other files.  Enabling HookJumps 2&3 will cause the HookJump 2 transition to repeat.

# Architecture

The main difference between playback of an `iMUSE file` and a standard midi file is that a "Director" is added to control the way the MIDI sequencer interprets a MIDI sequence.  The director can be thought of as part of the game code (and not direclty part of the music code).  Its role is to monitor the game state for events (or be notified by events..) that should result in playback changes.  The director then changes a set of flags in the sequencer, but the sequencer does not implement those changes until specific markers are encountered in the MIDI sequence.

## AudioSynthesis.Sequencer.IMuse.Sequencer

This class implements interactive MIDI sequencing for CSharpSynth project.  It reads a stream of MIDI events, applies a set of filters to each event that interpret any iMUSE SysEx events (0x7D) appropriately, and feeds them to the synthesizer at the appropriate playback time.

### HookJumps

Jump markers are musical `GOTO`s.  When the sequencer encounters a jump marker, it will consult a list of enabled jump IDs, and if the ID is in the enabled ID list it will immediately change sequencing to the specified track/beat/tick position in the file.

By default, the sequencer will _follow_ any marker with ID 0, and _ignore_ any marker with a non-zero ID.  The director can enable or disable specific IDs changing `HookJumpsEnabled`.

	# Follow Jump with id 2 next time it is encountered
    sequencer.HookJumpsEnabled[2] = false
	# Follow Jump with id 3 any time it is encountered
    sequencer.HookJumpsEnabled[3] = true

When a HookJump is followed, the sequencer will temporarily sequence _both_ the MIDI events after the jump marker _and_ events after the jump destination.  Sometimes a HookJump can occur after a NoteOn event but before the corresponding NoteOff event.  Any new NoteOn events after the jump marker are ignored.  However, NoteOff events after the marker are executed if (and only if) the note was playing when the jump was encountered.  This behavior allows overlapping notes before and after the jump.  (Internally, it's treated as two separate compositions played simultaniously, even if the source and destination are inside the same track.  The old one goes into a "WindDown" state, and is merged with the new one "Dovetail")

### Markers

Markers are callbacks that the music composer can use to tell the game code when something interesting has happened with the music.  Game code can decide what the marker means (or ignore it).

When the sequencer encounters a marker, it will call the `OnMarkerSequenced(ID)`.  What the IDs mean exactly is normally worked out between the composer and the developers.  For example, if an animation start is supposed to be synchronised with a specific musical note, the director can start the animation when the sequencer encounters the marker in the file.  The exact timing of the animation can thus be controlled by the composer.

Often this is used to signal the end of some transitional music segment.  For example, the director enables HookJump ID 1, then the sequencer eventually jumps (at a beat where HookJump 1 marker exists) to a transitional (musical bridge) segment (possibly in another track in the same file), the transitional music plays, then a marker is used to tell the director that the transitional segment is completed.  The exact amount of time to complete the transitional segment is not needed to be known in advance.  The director can then load another MIDI file, change game scenes, etc.

Note that `OnMarkerSequenced` is called from the Unity audio thread.  You may need to schedule any Unity API interaction to happen later on the main theread.

### Parts (hook)

A part is basically a disablable MIDI channel.  The idea is to allow the director to turn a channel on/off, but only at a time that makes musical sense to the composer.  The director tells the sequencer if it _wants_ a channel on or off, and the sequencer will disable it when a "part hook" is encountered.

The main interface for parts is:  `Sequencer.PartFilter.ChannelSettings[channelNumber].wantEnable`

Setting `wantEnable` to `true` will turn the channel on when a `IMuseSysExType.HookPartSetActive` marker is encountered.

The composer can also force a part to be enabled/disabled by using a marker with hookId of 0.  This is normally used at the beginning of the first track to initialize the default part state.  The director can then later have it turn it on or off based on game context.

## IMuseDirector

This is an example MonoBehavior implementing a "director."  It manages the sequencer/synthesizer, loads the patch bank, connects the output into Unity's Audio system as an AudioSource, and contains basic file loading functions.  Subclass, modify, or call from game code to suit your needs.
