package io.github.kawase.database.entity.enums;

import lombok.Getter;
import lombok.RequiredArgsConstructor;

@Getter
@RequiredArgsConstructor
public enum DefaultTaskType {
    // C++ Starter Quiz (multiple-choice)
    CPP_QUIZ("C++ Starter Quiz: Complete All Questions", 25),

    // C++ Code Debugging - Medium
    CPP_DEBUG_MEDIUM_MULTIPLY("C++ Debug: Fix MultiplyByTwo Function", 15),
    CPP_DEBUG_MEDIUM_SUM("C++ Debug: Fix Sum Operator Bug", 15),
    CPP_DEBUG_MEDIUM_EVEN("C++ Debug: Fix Even Number Check", 15),
    CPP_DEBUG_MEDIUM_INCREMENT("C++ Debug: Fix Pass-by-Reference", 20),

    // C++ Code Challenges - Hard
    CPP_HARD_IS_EVEN("C++ Challenge: Write IsEven Function", 30),
    CPP_HARD_MAX("C++ Challenge: Write MaxOfTwo Function", 30),
    CPP_HARD_SQUARE("C++ Challenge: Write Square Function", 30),
    CPP_HARD_SUM3("C++ Challenge: Write Sum3 Function", 30),
    CPP_HARD_FACTORIAL("C++ Challenge: Write Factorial3 Function", 35),

    // Python Practice - Medium
    PY_MEDIUM_MULTIPLY("Python Practice: Multiply By Two", 15),
    PY_MEDIUM_SUM("Python Practice: Add Function", 15),
    PY_MEDIUM_EVEN("Python Practice: Even Check", 15),
    PY_MEDIUM_LOOP("Python Practice: Loop Sum", 20),

    // Python Visual - Hard
    PY_HARD_BAR_LINE("Python Visual: Draw Bar Line", 30),
    PY_HARD_PROGRESS("Python Visual: Build Progress Bar", 30),
    PY_HARD_SQUARE("Python Visual: Draw Square Grid", 30),
    PY_HARD_STAIRS("Python Visual: Draw Staircase", 30),
    PY_HARD_ALTERNATING("Python Visual: Alternating Pattern", 35),

    // Logic Puzzles - Island 3 (variable manipulation)
    LOGIC_JUMP_AND_BOX("Logic Puzzle: Set Jump Power & Enable Physics", 20),
    LOGIC_REVEAL_ISLAND("Logic Puzzle: Reveal Hidden Island", 20),
    LOGIC_REVEAL_BRIDGE("Logic Puzzle: Reveal Bridge Path", 20);

    private final String title;
    private final int pointValue;
}
