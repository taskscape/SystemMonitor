const machineSelect = document.getElementById('machineSelect');
const refreshBtn = document.getElementById('refreshBtn');
const restartBtn = document.getElementById('restartBtn');
const statusChip = document.getElementById('statusChip');
const cpuNow = document.getElementById('cpuNow');
const ramNow = document.getElementById('ramNow');
const diskNow = document.getElementById('diskNow');
const lastSeen = document.getElementById('lastSeen');
const driveList = document.getElementById('driveList');

const cpuChart = document.getElementById('cpuChart');
const ramChart = document.getElementById('ramChart');
const diskChart = document.getElementById('diskChart');

// Store chart data for hover handling
const chartsData = new Map();

// Initialize chart interactions
[cpuChart, ramChart, diskChart].forEach(canvas => {
  canvas.addEventListener('mousemove', (e) => handleChartHover(e, canvas));
  canvas.addEventListener('mouseleave', () => {
    const data = chartsData.get(canvas);
    if (data) {
      // Redraw clean chart without highlight
      renderChart(canvas, data.points, { color: data.color });
    }
  });
});

function handleChartHover(e, canvas) {
  const data = chartsData.get(canvas);
  if (!data || data.points.length < 2) return;

  const rect = canvas.getBoundingClientRect();
  // Scale mouse coordinates to canvas internal resolution
  const mouseX = (e.clientX - rect.left) * (canvas.width / rect.width);
  
  const width = canvas.width;
  const plotWidth = width - 20;
  
  // Calculate nearest point index
  // x = 10 + (index / (N-1)) * plotWidth
  // index ~= (x - 10) / plotWidth * (N-1)
  const indexFloat = ((mouseX - 10) / plotWidth) * (data.points.length - 1);
  let index = Math.round(indexFloat);
  
  // Clamp to array bounds
  if (index < 0) index = 0;
  if (index >= data.points.length) index = data.points.length - 1;

  // Redraw with highlight
  renderChart(canvas, data.points, { color: data.color, highlightIndex: index });
}

refreshBtn.addEventListener('click', () => {
  loadMachines(true);
});

restartBtn.addEventListener('click', async () => {
  const machineName = machineSelect.value;
  if (!machineName) return;

  if (!confirm(`Are you sure you want to RESTART machine "${machineName}"?`)) {
    return;
  }

  try {
    setStatus('Sending Command...');
    const response = await fetch(`/api/v1/machines/${encodeURIComponent(machineName)}/commands`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ commandType: 'restart' })
    });

    if (response.ok || response.status === 202) {
      alert('Restart command sent successfully!');
    } else {
      alert('Failed to send command.');
    }
  } catch (err) {
    console.error(err);
    alert('Error sending command.');
  } finally {
    setStatus('Ready');
  }
});

machineSelect.addEventListener('change', () => {
  loadSelection();
});

function setStatus(text) {
  statusChip.textContent = text;
}

async function loadMachines(force = false) {
  setStatus('Syncing');
  try {
    const currentSelection = machineSelect ? machineSelect.value : null;
    
    const response = await fetch('/api/v1/machines', { cache: force ? 'reload' : 'default' });
    if (!response.ok) {
      throw new Error('Failed to load machines');
    }

    const machines = await response.json();
    if (!machineSelect) return;

    machineSelect.innerHTML = '';

    let foundCurrent = false;
    machines.forEach((machine) => {
      const option = document.createElement('option');
      option.value = machine.machineName;
      option.textContent = machine.machineName;
      machineSelect.appendChild(option);
      if (machine.machineName === currentSelection) {
        foundCurrent = true;
      }
    });

    if (machines.length > 0) {
      if (foundCurrent) {
        machineSelect.value = currentSelection;
      } else {
        machineSelect.value = machines[0].machineName;
      }
      await loadSelection();
    } else {
      resetDashboard();
    }
    setStatus('Ready');
  } catch (err) {
    console.error('Load machines error:', err);
    setStatus('Offline');
    resetDashboard();
  }
}

async function loadSelection() {
  const machineName = machineSelect.value;
  
  if (!machineName) {
    restartBtn.style.display = 'none';
    resetDashboard();
    return;
  }
  
  restartBtn.style.display = 'inline-block';

  setStatus('Loading');
  try {
    const [currentResponse, historyResponse] = await Promise.all([
      fetch(`/api/v1/machines/${encodeURIComponent(machineName)}/current`),
      fetch(`/api/v1/machines/${encodeURIComponent(machineName)}/history?days=7`)
    ]);

    if (!currentResponse.ok || !historyResponse.ok) {
      throw new Error('Missing data');
    }

    const current = await currentResponse.json();
    const history = await historyResponse.json();

    updateCurrent(current);
    updateDrives(current.drives || []);
    updateCharts(history);
    setStatus('Ready');
  } catch (err) {
    console.error(err);
    setStatus('Unavailable');
    resetDashboard();
  }
}

function updateCurrent(current) {
  const ramPercent = percent(current.ramUsedBytes, current.ramTotalBytes);
  const driveTotals = (current.drives || []).reduce(
    (acc, drive) => {
      acc.used += drive.usedBytes;
      acc.total += drive.totalBytes;
      return acc;
    },
    { used: 0, total: 0 }
  );
  const drivePercent = percent(driveTotals.used, driveTotals.total);

  cpuNow.textContent = `${current.cpuPercent.toFixed(1)}%`;
  ramNow.textContent = `${ramPercent.toFixed(1)}%`;
  diskNow.textContent = `${drivePercent.toFixed(1)}%`;
  
  const dateObj = new Date(current.timestampUtc);
  const timeStr = dateObj.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  const day = String(dateObj.getDate()).padStart(2, '0');
  const month = String(dateObj.getMonth() + 1).padStart(2, '0');
  const year = dateObj.getFullYear();
  const dateStr = `${day}.${month}.${year}`;

  lastSeen.innerHTML = `
    <div>${timeStr}</div>
    <div style="font-size: 0.6em; opacity: 0.7; margin-top: -2px;">${dateStr}</div>
  `;
}

function updateDrives(drives) {
  driveList.innerHTML = '';
  if (!drives.length) {
    driveList.innerHTML = '<p class="subtitle">No drive samples.</p>';
    return;
  }

  drives.forEach((drive) => {
    const card = document.createElement('div');
    card.className = 'drive-card';
    const usedPercent = percent(drive.usedBytes, drive.totalBytes);
    card.innerHTML = `
      <strong>${drive.name}</strong>
      <span>${formatBytes(drive.usedBytes)} / ${formatBytes(drive.totalBytes)}</span>
      <span>${usedPercent.toFixed(1)}% used</span>
    `;
    driveList.appendChild(card);
  });
}

function updateCharts(history) {
  if (!history.length) {
    renderChart(cpuChart, [], { color: '#e4572e' });
    renderChart(ramChart, [], { color: '#2d9cdb' });
    renderChart(diskChart, [], { color: '#1b998b' });
    return;
  }

  const cpuPoints = history.map((item) => ({
    x: new Date(item.timestampUtc),
    y: item.cpuPercent
  }));

  const ramPoints = history.map((item) => ({
    x: new Date(item.timestampUtc),
    y: percent(item.ramUsedBytes, item.ramTotalBytes)
  }));

  const diskPoints = history.map((item) => ({
    x: new Date(item.timestampUtc),
    y: percent(item.driveUsedBytes, item.driveTotalBytes)
  }));

  // Save data to cache for mouse events
  chartsData.set(cpuChart, { points: cpuPoints, color: '#e4572e' });
  chartsData.set(ramChart, { points: ramPoints, color: '#2d9cdb' });
  chartsData.set(diskChart, { points: diskPoints, color: '#1b998b' });

  renderChart(cpuChart, cpuPoints, { color: '#e4572e' });
  renderChart(ramChart, ramPoints, { color: '#2d9cdb' });
  renderChart(diskChart, diskPoints, { color: '#1b998b' });
}

function renderChart(canvas, points, { color, highlightIndex }) {
  const ctx = canvas.getContext('2d');
  const width = canvas.width;
  const height = canvas.height;
  ctx.clearRect(0, 0, width, height);

  // Background
  ctx.fillStyle = '#faf6f1';
  ctx.fillRect(0, 0, width, height);

  // Grid lines
  ctx.strokeStyle = '#e4ded5';
  ctx.lineWidth = 1;
  for (let i = 0; i <= 4; i += 1) {
    const y = (height - 20) - (i / 4) * (height - 40) + 10;
    ctx.beginPath();
    ctx.moveTo(10, y);
    ctx.lineTo(width - 10, y);
    ctx.stroke();
  }

  if (points.length < 2) {
    ctx.fillStyle = '#c0b7aa';
    ctx.font = '12px "Space Grotesk", sans-serif';
    ctx.fillText('Awaiting data', 16, height / 2);
    return;
  }

  const plotWidth = width - 20;
  const plotHeight = height - 20;
  const yMin = 0;
  const yMax = 100;

  const getX = (index) => 10 + (index / (points.length - 1)) * plotWidth;
  const getY = (val) => height - 10 - ((val - yMin) / (yMax - yMin)) * plotHeight;

  ctx.strokeStyle = color;
  ctx.lineWidth = 3;
  ctx.lineJoin = 'round';
  ctx.beginPath();

  ctx.moveTo(getX(0), getY(points[0].y));

  for (let i = 0; i < points.length - 1; i++) {
    const xMid = (getX(i) + getX(i + 1)) / 2;
    const yMid = (getY(points[i].y) + getY(points[i + 1].y)) / 2;
    ctx.quadraticCurveTo(getX(i), getY(points[i].y), xMid, yMid);
    ctx.quadraticCurveTo(getX(i + 1), getY(points[i + 1].y), getX(i + 1), getY(points[i + 1].y));
  }
  ctx.stroke();

  // Gradient
  const gradient = ctx.createLinearGradient(0, 0, 0, height);
  gradient.addColorStop(0, `${color}33`);
  gradient.addColorStop(1, `${color}00`);
  ctx.lineTo(width - 10, height - 10);
  ctx.lineTo(10, height - 10);
  ctx.closePath();
  ctx.fillStyle = gradient;
  ctx.fill();

  // Interactive point (only if highlightIndex is defined)
  if (typeof highlightIndex === 'number') {
    const point = points[highlightIndex];
    const px = getX(highlightIndex);
    const py = getY(point.y);

    // Vertical line
    ctx.beginPath();
    ctx.moveTo(px, 10);
    ctx.lineTo(px, height - 10);
    ctx.strokeStyle = 'rgba(0,0,0,0.1)';
    ctx.lineWidth = 1;
    ctx.stroke();

    // Dot
    ctx.beginPath();
    ctx.arc(px, py, 7, 0, Math.PI * 2);
    ctx.fillStyle = '#fff';
    ctx.fill();
    ctx.strokeStyle = color;
    ctx.lineWidth = 3;
    ctx.stroke();

    // Tooltip
    const timeStr = new Date(point.x).toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'});
    const valStr = `${point.y.toFixed(1)}%`;
    const text = `${timeStr} | ${valStr}`;
    
    ctx.font = 'bold 18px "Space Grotesk", sans-serif';
    const textWidth = ctx.measureText(text).width;
    const padding = 14;
    const boxHeight = 36;
    let boxX = px - textWidth / 2 - padding;
    let boxY = py - 50;

    // Safety check to keep within screen bounds
    if (boxX < 0) boxX = 5;
    if (boxX + textWidth + 2 * padding > width) boxX = width - textWidth - 2 * padding - 5;
    if (boxY < 5) boxY = py + 25;

    ctx.fillStyle = 'rgba(16, 19, 31, 0.95)';
    ctx.beginPath();
    ctx.roundRect(boxX, boxY, textWidth + 2 * padding, boxHeight, 8);
    ctx.fill();

    ctx.fillStyle = '#fff';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, boxX + padding, boxY + boxHeight / 2);
  }
}

function resetDashboard() {
  cpuNow.textContent = '--';
  ramNow.textContent = '--';
  diskNow.textContent = '--';
  lastSeen.textContent = '--';
  driveList.innerHTML = '<p class="subtitle">No machine selected.</p>';
  renderChart(cpuChart, [], { color: '#e4572e' });
  renderChart(ramChart, [], { color: '#2d9cdb' });
  renderChart(diskChart, [], { color: '#1b998b' });
}

function percent(used, total) {
  if (!total) {
    return 0;
  }
  return (used / total) * 100;
}

function formatBytes(bytes) {
  if (!bytes) {
    return '0 B';
  }
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let value = bytes;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  return `${value.toFixed(1)} ${units[unitIndex]}`;
}

loadMachines();

// Auto-refresh every 30 seconds
setInterval(() => loadMachines(true), 30000);
