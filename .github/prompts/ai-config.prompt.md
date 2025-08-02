---
mode: agent
---
# Project Overview
ScarletQuests is a modding project for V Rising, focused on custom quest systems, NPCs, and event-driven gameplay extensions. It uses ScarletCore as a base framework.

# Folder Structure
- `/Models`: Contains quest, objective, and NPC models.
- `/Systems`: Core systems for quest logic and event handling.
- `/Services`: Utility and helper services for game integration.
- `/Definitions`: Game data definitions and types.
- `/Commands`: Command handlers and test scripts.

# Libraries and Frameworks
- ScarletCore (see https://github.com/markvaaz/ScarletCore)
- ProjectM (V Rising modding API)
- Unity.Entities, Unity.Mathematics

# Coding Standards
- Use C# 10+ features and modern syntax.
- Prefer explicit types over `var` except for LINQ and obvious cases.
- Use PascalCase for class, method, and property names.
- Use camelCase for local variables and parameters.
- Keep code and comments in English.

Rules:
1- Do not change anything except what is requested, always do the minimum necessary.
2- Do not add comments unless they are in English.
3- If you are not sure how something works or how to use it, ask me before making any changes.
4- If I ask you to solve or explain a problem, do not delete the problem, just try to resolve it.
5- Whenever you have questions about how any ScarletCore system works, you can access the GitHub page: https://github.com/markvaaz/ScarletCore