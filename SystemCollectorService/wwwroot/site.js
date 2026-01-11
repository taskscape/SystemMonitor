const machineSelect = document.getElementById('machineSelect');
const refreshBtn = document.getElementById('refreshBtn');
const statusChip = document.getElementById('statusChip');
const cpuNow = document.getElementById('cpuNow');
const ramNow = document.getElementById('ramNow');
const diskNow = document.getElementById('diskNow');
const lastSeen = document.getElementById('lastSeen');
const driveList = document.getElementById('driveList');

const cpuChart = document.getElementById('cpuChart');
const ramChart = document.getElementById('ramChart');
const diskChart = document.getElementById('diskChart');

refreshBtn.addEventListener('click', () => {
  loadMachines(true);
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
    const response = await fetch('/api/v1/machines', { cache: force ? 'reload' : 'default' });
    if (!response.ok) {
      throw new Error('Failed to load machines');
    }

    const machines = await response.json();
    machineSelect.innerHTML = '';

    machines.forEach((machine) => {
      const option = document.createElement('option');
      option.value = machine.machineName;
      option.textContent = machine.machineName;
      machineSelect.appendChild(option);
    });

    if (machines.length > 0) {
      machineSelect.value = machines[0].machineName;
      await loadSelection();
    } else {
      resetDashboard();
    }
    setStatus('Ready');
  } catch (err) {
    console.error(err);
    setStatus('Offline');
    resetDashboard();
  }
}

async function loadSelection() {
  const machineName = machineSelect.value;
  if (!machineName) {
    resetDashboard();
    return;
  }

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
  lastSeen.textContent = new Date(current.timestampUtc).toISOString();
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

  renderChart(cpuChart, cpuPoints, { color: '#e4572e' });
  renderChart(ramChart, ramPoints, { color: '#2d9cdb' });
  renderChart(diskChart, diskPoints, { color: '#1b998b' });
}

function renderChart(canvas, points, { color }) {
  const ctx = canvas.getContext('2d');
  const width = canvas.width;
  const height = canvas.height;
  ctx.clearRect(0, 0, width, height);

  ctx.fillStyle = '#faf6f1';
  ctx.fillRect(0, 0, width, height);

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

  ctx.strokeStyle = color;
  ctx.lineWidth = 2;
  ctx.beginPath();

  points.forEach((point, index) => {
    const x = 10 + (index / (points.length - 1)) * plotWidth;
    const y = height - 10 - ((point.y - yMin) / (yMax - yMin)) * plotHeight;
    if (index === 0) {
      ctx.moveTo(x, y);
    } else {
      ctx.lineTo(x, y);
    }
  });

  ctx.stroke();

  const gradient = ctx.createLinearGradient(0, 0, 0, height);
  gradient.addColorStop(0, `${color}33`);
  gradient.addColorStop(1, `${color}00`);

  ctx.lineTo(width - 10, height - 10);
  ctx.lineTo(10, height - 10);
  ctx.closePath();
  ctx.fillStyle = gradient;
  ctx.fill();
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
