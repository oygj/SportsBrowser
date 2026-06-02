const datePicker = document.getElementById('datePicker');
const refreshButton = document.getElementById('refreshButton');
const prevDayButton = document.getElementById('prevDayButton');
const todayButton = document.getElementById('todayButton');
const nextDayButton = document.getElementById('nextDayButton');
const statusMessage = document.getElementById('statusMessage');
const gamesContainer = document.getElementById('gamesContainer');

const now = new Date();
datePicker.value = formatDateValue(now);

refreshButton.addEventListener('click', () => {
  loadSchedule(datePicker.value);
});

prevDayButton.addEventListener('click', () => {
  const current = new Date(datePicker.value);
  current.setDate(current.getDate() - 1);
  datePicker.value = formatDateValue(current);
  loadSchedule(datePicker.value);
});

todayButton.addEventListener('click', () => {
  const today = new Date();
  datePicker.value = formatDateValue(today);
  loadSchedule(datePicker.value);
});

nextDayButton.addEventListener('click', () => {
  const current = new Date(datePicker.value);
  current.setDate(current.getDate() + 1);
  datePicker.value = formatDateValue(current);
  loadSchedule(datePicker.value);
});

datePicker.addEventListener('change', () => {
  if (datePicker.value) {
    loadSchedule(datePicker.value);
  }
});

window.addEventListener('load', () => {
  loadSchedule(datePicker.value);
});

async function loadSchedule(dateValue) {
  showStatus('Loading schedule...', '');
  gamesContainer.innerHTML = '';

  try {
    const url = `/api/games?date=${encodeURIComponent(dateValue)}`;
    const response = await fetch(url);

    if (!response.ok) {
      throw new Error(`Server returned ${response.status}`);
    }

    const games = await response.json();

    if (!Array.isArray(games) || games.length === 0) {
      showStatus('No Philadelphia games were found for this date.', '');
      return;
    }

    renderGames(games);
    showStatus(`Showing ${games.length} game(s) for ${formatDisplayDate(dateValue)}`);
  } catch (error) {
    showStatus('Unable to load schedule. Please try again later.', error.message);
  }
}

function renderGames(games) {
  gamesContainer.innerHTML = '';

  games.forEach((game) => {
    const resultClass = getResultClass(game);
    const card = document.createElement('article');
    card.className = `game-card${resultClass ? ` ${resultClass}` : ''}`;
    if (game.logoUrl) {
      card.style.setProperty('--logo-url', `url("${game.logoUrl}")`);
    }

    card.innerHTML = `
      <header>
        <div>
          <h2>${game.sport} — ${game.teamDisplayName}</h2>
          <div class="status-badge ${getStatusClass(game.status, game.statusDetail)}">${formatStatusLabel(game.status, game.statusDetail)}</div>
        </div>
        <div>${formatTime(game.startDate)}</div>
      </header>
      <div class="matchup-section">
        <div class="team-box away">
          <div class="team-name">${game.awayTeam}</div>
          <div class="team-abbr">${game.awayTeamAbbreviation}</div>
        </div>
        <div class="score-display">
          <span class="score">${formatScore(game.awayScore, game.homeScore, game.status, game.statusDetail)}</span>
        </div>
        <div class="team-box home">
          <div class="team-name">${game.homeTeam}</div>
          <div class="team-abbr">${game.homeTeamAbbreviation}</div>
        </div>
      </div>
      <div class="game-meta">
        <div class="venue-row"><span class="team-label">Venue</span><span>${game.venue}</span></div>
        <div class="venue-row"><span class="team-label">Opponent</span><span>${game.opponentDisplayName}</span></div>
        ${game.eventUrl ? `<div class="venue-row"><span class="team-label">Game Link</span><a href="${game.eventUrl}" target="_blank" rel="noreferrer">Open details</a></div>` : ''}
      </div>
      ${game.boxScore ? renderBoxScore(game.boxScore) : ''}
    `;

    gamesContainer.appendChild(card);
  });
}

function showStatus(message, details = '') {
  statusMessage.textContent = message;
  if (details) {
    statusMessage.textContent += ` ${details}`;
  }
}

function formatDateValue(date) {
  return date.toISOString().slice(0, 10);
}

function formatDisplayDate(value) {
  const [year, month, day] = value.split('-');
  const date = new Date(year, month - 1, day);
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
}

function formatTime(value) {
  const date = new Date(value);
  return date.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
}

function isScheduledStatus(status, detail) {
  const combined = `${status ?? ''} ${detail ?? ''}`.toLowerCase();

  return /\b(?:scheduled|pre(?:-| )?game|pregame|preview|upcoming|pending|tbd|not started|future)\b/.test(combined);
}

function formatScore(away, home, status, detail) {
  const awayValue = Number(away);
  const homeValue = Number(home);
  const isNumericScore = Number.isFinite(awayValue) && Number.isFinite(homeValue);

  if (isNumericScore && awayValue === 0 && homeValue === 0 && isScheduledStatus(status, detail)) {
    return 'vs.';
  }

  if (!isNumericScore || away == null || home == null) {
    return 'Not available';
  }

  return `${awayValue} – ${homeValue}`;
}

function formatStatusLabel(status, detail) {
  const normalizedStatus = String(status ?? '').trim();
  const normalizedDetail = String(detail ?? '').trim();

  if (!normalizedStatus && !normalizedDetail) {
    return 'Unknown';
  }

  return getReadableStatus(normalizedStatus, normalizedDetail);
}

function getReadableStatus(status, detail) {
  const combined = `${status} ${detail}`.toLowerCase();

  if (/final|ft|full time/.test(combined)) return 'Final';
  if (/in progress|inprogress|live/.test(combined)) return 'Live';
  if (/scheduled/.test(combined)) return 'Scheduled';
  if (/pre|pregame|preview/.test(combined)) return 'Pre-game';
  if (/postponed/.test(combined)) return 'Postponed';
  if (/cancelled|canceled/.test(combined)) return 'Cancelled';
  if (/delayed/.test(combined)) return 'Delayed';
  if (/warmup/.test(combined)) return 'Warm-up';
  if (/halftime|half time/.test(combined)) return 'Halftime';

  if (detail) return titleCase(detail);
  return titleCase(status);
}

function titleCase(text) {
  return text
    .toLowerCase()
    .replace(/\b([a-z])/g, (match) => match.toUpperCase())
    .replace(/\s+/g, ' ')
    .trim();
}

function isFinalStatus(status, detail) {
  const normalizedStatus = String(status ?? '').toLowerCase();
  const normalizedDetail = String(detail ?? '').toLowerCase();
  return normalizedStatus.includes('final')
    || normalizedStatus.includes('ft')
    || normalizedDetail.includes('final')
    || normalizedDetail.includes('ft')
    || normalizedDetail.includes('full time');
}

function getResultClass(game) {
  if (game.homeScore == null || game.awayScore == null) {
    return '';
  }

  if (!isFinalStatus(game.status, game.statusDetail)) {
    return '';
  }

  const isHome = Boolean(game.isHome ?? game.IsHome);
  const philadelphiaScore = isHome ? game.homeScore : game.awayScore;
  const opponentScore = isHome ? game.awayScore : game.homeScore;

  if (philadelphiaScore == null || opponentScore == null) {
    return '';
  }

  if (philadelphiaScore > opponentScore) return 'win';
  if (philadelphiaScore < opponentScore) return 'loss';
  return 'draw';
}

function getStatusClass(status, detail) {
  if (!status && !detail) return 'status-other';
  const normalizedStatus = String(status ?? '').toLowerCase();
  const normalizedDetail = String(detail ?? '').toLowerCase();

  if (isFinalStatus(status, detail)) return 'status-final';
  if (normalizedStatus.includes('in progress') || normalizedStatus.includes('inprogress') || normalizedStatus.includes('live')
    || normalizedDetail.includes('in progress') || normalizedDetail.includes('inprogress') || normalizedDetail.includes('live')) {
    return 'status-inprogress';
  }
  if (normalizedStatus.includes('scheduled') || normalizedStatus.includes('pre') || normalizedStatus.includes('preview')
    || normalizedDetail.includes('scheduled') || normalizedDetail.includes('pre') || normalizedDetail.includes('preview')) {
    return 'status-scheduled';
  }

  return 'status-other';
}

function renderBoxScore(boxScore) {
  if (!boxScore || (!boxScore.homeTeam && !boxScore.awayTeam)) {
    return '';
  }

  const away = boxScore.awayTeam;
  const home = boxScore.homeTeam;

  return `
    <div class="box-score">
      <h3>Box Score</h3>
      <table class="stats-table">
        <thead>
          <tr>
            <th>Stat</th>
            <th class="away-stat">${away?.teamName || 'Away'}</th>
            <th class="home-stat">${home?.teamName || 'Home'}</th>
          </tr>
        </thead>
        <tbody>
          ${renderStatRow('FG', away?.fieldGoalsMade, away?.fieldGoalsAttempted, home?.fieldGoalsMade, home?.fieldGoalsAttempted)}
          ${renderStatRow('3P', away?.threePointersMade, away?.threePointersAttempted, home?.threePointersMade, home?.threePointersAttempted)}
          ${renderStatRow('FT', away?.freeThrowsMade, away?.freeThrowsAttempted, home?.freeThrowsMade, home?.freeThrowsAttempted)}
          ${renderStatRow('REB', away?.rebounds, null, home?.rebounds, null)}
          ${renderStatRow('AST', away?.assists, null, home?.assists, null)}
          ${renderStatRow('TO', away?.turnovers, null, home?.turnovers, null)}
          ${renderStatRow('STL', away?.steals, null, home?.steals, null)}
          ${renderStatRow('BLK', away?.blocks, null, home?.blocks, null)}
          ${renderStatRow('PF', away?.fouls, null, home?.fouls, null)}
        </tbody>
      </table>
    </div>
  `;
}

function renderStatRow(label, awayStat, awayTotal, homeStat, homeTotal) {
  const awayValue = awayStat !== null && awayTotal !== null ? `${awayStat}-${awayTotal}` : (awayStat !== null ? awayStat : '—');
  const homeValue = homeStat !== null && homeTotal !== null ? `${homeStat}-${homeTotal}` : (homeStat !== null ? homeStat : '—');
  
  return `
    <tr>
      <td class="stat-label">${label}</td>
      <td class="away-stat">${awayValue}</td>
      <td class="home-stat">${homeValue}</td>
    </tr>
  `;
}
