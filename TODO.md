# TODO LIST

- implement TTS for LLM generated interactions
    - call an OpenAI compatible API to generate TTS (something like Kokoro-FastAPI)
    - retrieve the voices at the start of the game
    - assign different voices to each pawn, match female/male voices accordingly (user customization later)
    - generate the TTS for every dialogue line using the right voice, pregenerate the next line while the current one is playing
    - add a setting to the menu bar to disable TTS (interrupts ongoing voiceline too)