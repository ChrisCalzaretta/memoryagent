# 🎯 Where to Find the Job Status Extension

## Exact Location: BOTTOM-LEFT Corner

The status bar is at the **absolute bottom** of your Cursor window:

```
┌─────────────────────────────────────────────────────────────┐
│  File  Edit  Selection  View  ...                          │  ← Menu bar (top)
├─────────────────────────────────────────────────────────────┤
│  Explorer | Search | Extensions | ...                      │  ← Sidebar (left)
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  YOUR CODE EDITOR                                           │
│  (this is where you edit files)                             │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│  💤 No active jobs  │  Ln 89, Col 1  │  Spaces: 2  │ UTF-8│  ← STATUS BAR (bottom) 👈 LOOK HERE!
└─────────────────────────────────────────────────────────────┘
   ↑
   LEFT SIDE of bottom bar - that's where it shows!
```

---

## 🔍 What You Should See

### When No Jobs:
```
💤 No active jobs
```

### When Job is Running:
```
🔄 Calculator (30%) | ⏱️ 45s
```

### Multiple Jobs:
```
🔄 2 jobs | Calculator (60%)
```

---

## ✅ CHECKLIST - Did You Do This?

- [ ] **1. Reload Cursor**
  - Press: `Ctrl + Shift + P`
  - Type: `reload`
  - Select: `Developer: Reload Window`
  - Wait for restart (5-10 seconds)

- [ ] **2. Look at the BOTTOM-LEFT**
  - Not top, not middle - the very bottom of the window
  - Left side of the status bar
  - Should see the pulse icon: 💤 or 🔄

- [ ] **3. If Still Not Visible**
  - Check Output panel (next section)

---

## 🐛 Troubleshooting: Check Extension Logs

### Step 1: Open Output Panel
1. Click `View` menu (top menu bar)
2. Click `Output`
3. A panel opens at the bottom

### Step 2: Select Extension Host
In the Output panel, there's a dropdown on the right that says "Tasks" or similar.
Click it and select: **`Extension Host`**

### Step 3: Look for Extension Messages
You should see logs like:
```
[Job Status] Extension activating...
[Job Status] Workspace: E:\GitHub\MemoryAgent
[Job Status] Context: memoryagent
[Job Status] Orchestrator: http://localhost:5001
[Job Status] Extension activated successfully!
```

### If You See Errors:
Copy the error message and let me know!

---

## 🎬 Alternative: Use Command Palette

If the status bar isn't showing, you can still access the extension via commands:

1. Press `Ctrl + Shift + P`
2. Type: `Show Job Details`
3. Press Enter

If the extension is loaded, this command should work (even if status bar isn't visible).

---

## 🔄 Still Not Working?

Try these in order:

### Option 1: Full Restart
1. Close **ALL** Cursor windows (completely quit Cursor)
2. Reopen Cursor
3. Check status bar again

### Option 2: Check Extension Is Enabled
1. Press `Ctrl + Shift + P`
2. Type: `Extensions: Show Installed Extensions`
3. Look for "Cursor Job Status"
4. Make sure it's enabled (not disabled)

### Option 3: Reinstall
```powershell
.\install-job-status-extension.ps1
```
Then restart Cursor completely.

---

## 📸 Visual Reference

Your Cursor window has these sections from top to bottom:

1. **Menu Bar** (File, Edit, etc.) - TOP
2. **Title Bar** (file path)
3. **Editor Area** (where you type code) - MIDDLE
4. **Panel Area** (Terminal, Output, etc.) - BOTTOM MIDDLE (optional)
5. **STATUS BAR** (blue/purple bar) - ABSOLUTE BOTTOM 👈 HERE!

The status bar is always visible and is the last thing at the bottom.

---

## 💡 Quick Test

After reloading, try this in Cursor chat:

```
Press Ctrl + Shift + P
Type: "show job"
```

You should see: `Show Job Details` command

If that command exists, the extension loaded successfully!

---

## ❓ Common Confusions

**"I'm looking at the bottom panel"** ❌
- That's the Terminal/Output panel (middle area)
- Look **below** that - the thin bar at the very bottom

**"I see line numbers"** ❌
- That's in the editor area (middle)
- Look at the **window's absolute bottom edge**

**"I see the sidebar"** ❌
- That's the left side (Explorer, Search, etc.)
- Look at the **bottom of the window**, left corner of status bar

---

## 🎯 The Bottom Line

The status bar is:
- At the **absolute bottom** of your Cursor window
- A thin **horizontal bar** (usually blue or purple)
- Shows things like **line numbers**, **language**, **encoding**
- Our extension adds **job status on the LEFT side** of that bar

---

**After you reload Cursor, take a screenshot of your entire Cursor window if you still don't see it, and I can point out exactly where to look!**


