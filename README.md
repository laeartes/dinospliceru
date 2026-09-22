# dinospliceru

"dinospliceru" is a video editing tool that combines a variety of features into one for a more efficient workflow. Rather than switching between several separate tools online, users can rely on "dinospliceru" as a single application that keeps their workflow smooth and efficient.

## "Tiesiog UAB" Team Members

- **Mykolas** ★ [@laeartes](https://github.com/laeartes)

- **Dinas** [@DinasZaranka](https://github.com/DinasZaranka)

- **Žygimantas** [@raubatronas](https://github.com/raubatronas)

- **Airidas** [@AiridasM](https://github.com/AiridasM)

- **Emilijus** [@Emilijus-Trinkunas](https://github.com/emilijus-trinkunas)


## Project

### Functionality

- Uploading selected videos

- Splicing videos by length or size

- Cutting videos

- Exporting videos in a chosen file format

- Setting custom parameters

- Preset saving within an account

- Video queueing

### Versions

**ALPHA**

- Video uploading

- Video splicing

- Ability to download finished product

---

**BETA**

- Video compression

- Custom parameters (resolution, CRF, etc.)

- User accounts (used to save presets, previous clips)

---

**FINAL**

- Ability to queue videos

- Statistics

- Polished product

### Technology Stack

**Frontend**
- **Core:** React + TypeScript (via Vite)
- **Styling:** Tailwind CSS

**Backend**
- **Runtime & Framework:** .NET 10 / ASP.NET Core Web API (C# 14)
- **Media Processing:** FFmpeg / `FFMpegCore` wrapper
- **Database:** PostgreSQL 


## Prerequisites

### Frontend

- **Node.js 24.x (LTS)** — [Download](https://nodejs.org/)

### Backend

- **.NET 10 SDK & ASP.NET Core Runtime** — [Download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- **FFmpeg** — must be installed and available on `PATH`

  | OS      | Installation |
  |---------|--------------|
  | Linux   | `sudo apt install ffmpeg` (Debian/Ubuntu), `sudo pacman -S ffmpeg` (Arch) |
  | macOS   | `brew install ffmpeg` |
  | Windows | `winget install ffmpeg` or download from [ffmpeg.org](https://ffmpeg.org/download.html) and add to `PATH` |

  Verify after install: `ffmpeg -version`

## Running the Program

### Frontend

```bash
# Navigate to the frontend directory
cd frontend

# Install dependencies
npm install

# Run dev server
npm run dev

# Build for production
npm run build

# Run tests (watch mode)
npm test

# Run tests once (non-watch mode, useful for CI)
npm run test:run

# Lint check
npm run lint
```

### Backend

```bash
# Build all projects
dotnet build

# Run API server
dotnet run --project src/DinoSplicer.Api

# Run tests
dotnet test

# Format check
dotnet format
```