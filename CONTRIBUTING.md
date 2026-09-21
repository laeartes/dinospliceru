# Contributing Guidelines

Welcome to the project and thank you for taking interest in contributing! To maintain code quality and ensure clean collaboration, all contributors must adhere to the following processes.

---

## 1. Issue Tracking Process

* **Coordination:** Every piece of work must be tracked via an Issue before development begins.
* **Kanban Workflow:**
  * **Backlog:** Newly created issues awaiting refinement.
  * **Ready:** Prioritized tasks with an assigned developer.
  * **In Progress:** Active development. Manually move the issue card here when you create your local/remote branch and begin working.
  * **In Review:** Automatically moved when a Pull Request is opened.
  * **Done:** Automatically moved when the linked PR is merged and the issue is closed.

---

## 2. Branch Naming Convention

All branches must be created from `main` and strictly follow the format below in all **lowercase**, using **hyphens** instead of spaces:

```text
<type>/<initials>/<issue-number>-<description>
```

### Allowed Types:
* `feature` - New features or functional additions.
* `bugfix` - Resolving a bug identified in development or testing.
* `hotfix` - Urgent fixes requiring rapid integration.
* `refactor` - Code restructuring without behavior changes.
* `chore` - Non-code tasks such as documentation, build scripts or configuration updates.
* `test` - Adding or updating test suites.

### Examples:
* `feature/diza/2069-timeline-scrubber`
* `bugfix/myme/34-fix-render-crash`
* `refactor/emtr/4204-cleanup-api-controllers`
* `test/myme/673-add-unit-tests`
* `chore/myme/3-contribution-file`

---

## 3. Commit Guidelines

* **Message Format:** Follow the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) structure in all lowercase:
  ```text
  <type>: <short imperative summary>
  ```
* **Allowed Types:** Matches the branch types (`feature`, `bugfix`, `hotfix`, `refactor`, `chore`, `test`).
* **Writing Rules:**
  * Use the imperative mood ("add", "fix", "change", not "added" or "fixes").
  * Do not capitalize the first letter after the colon.
  * Do not end the message with a period.
  * Keep the summary line under 72 characters.
* **Atomic Commits:** Each commit should represent a single logical change. Avoid monolithic commits with generic messages like `wip`, `fixes`, or `updates`.
### Examples:
* `feature: add basic timeline scrubbing canvas`
* `bugfix: resolve race condition on video render worker`
* `refactor: extract ffmpeg execution into separate service`
* `test: add unit tests for invalid clip splits`
* `chore: add contributing guidelines and issue templates`

---

## 4. Pull Request & Review Process

* **Target Branch:** All pull requests must target `main`.
* **Issue Linking:** Explicitly link the corresponding issue in the PR description using closure keywords:
  ```text
  Closes #<issue-number>
  ```
* **Review Requirements:**
  * **At least 2 team members must review and approve** the PR before it can be merged.
  * Authors must not merge their own code without the required approvals.
* **Pre-Merge Checklist:**
  * No merge conflicts with `main`.
  * Backend passes: `dotnet test` and `dotnet format --verify-no-changes`.
  * Frontend passes: `npm test`, `npm run lint`, and `npm run build`.
* **Merge Method:** Once approved and CI checks pass, merge via GitHub using **Squash and merge**. Ensure the squashed commit title follows the Conventional Commits format and references the PR number.
---

## 5. Coding Standards

### Backend 
* **Code Style:** Strictly follow the repository `.editorconfig`.
* **Formatting:** Run the formatter locally before staging commits:
  ```bash
  dotnet format
  ```
* **Language Rules:**
  * PascalCase for Classes, Records, Interfaces, Methods, and Public Properties.
  * camelCase for method arguments and local variables.
  * `_camelCase` for private fields.

### Frontend 
* **File Naming:** PascalCase for React components (`TimelineTrack.tsx`), camelCase for utilities/hooks (`useScrubber.ts`).
* **Linting:** Ensure `npm run lint` passes with zero errors before opening a PR.
* **Component Boundaries:** Keep heavy computations and canvas/timeline rendering decoupled from unnecessary React re-renders.

---

## 6. Testing Requirements

* **Backend Testing:**
  * Built using **xUnit** and **FluentAssertions**.
  * Test projects reside in the `tests/` directory mirroring source structure.
  * Use the standard naming pattern: `UnitOfWork_StateUnderTest_ExpectedBehavior`.
    * *Example:* `SplitClip_InvalidTimestamp_ThrowsArgumentException`
  * Run the full test suite before pushing:
    ```bash
    dotnet test
    ```    
* **Frontend Testing:**
  * Place all unit and component tests in the `tests/` directory, mirroring the `src/` folder structure, using the `.test.tsx` or `.test.ts` extension (e.g., `tests/components/TimelineTrack.test.tsx` for `src/components/TimelineTrack.tsx`).
  * Run frontend tests locally:
    ```bash
    npm test
    ```
  * Run frontend tests once (non-watch mode, useful for CI):
    ```bash
    npm run test:run
    ```
  * Optionally, run tests with an interactive browser UI:
    ```bash
    npm run test:ui
    ```
* **Test Media and Generated Files:**
  * Alongside the test projects, the `tests/` directory contains dedicated directories for shared test media and generated files.
  * Small media fixtures required by automated tests may be stored in `tests/assets/` and committed to the repository.
  * When local test media, generated output, or temporary files are needed, use `tests/assets/local/`, `tests/output/`, and `tests/tmp/` respectively.
  * The latter three directories are ignored by Git.
    
* **PR Coverage:** Any new business logic, API endpoint or bugfix must include corresponding unit tests covering happy paths and edge cases.
