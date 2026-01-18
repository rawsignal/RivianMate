import { useState, useEffect } from 'react';
import { Battery, Zap, Gauge, MapPin, ThermometerSun, Clock, TrendingDown, Shield, ChevronRight, Settings, Car } from 'lucide-react';

// Animated counter component
const AnimatedNumber = ({ value, decimals = 1, duration = 1000 }) => {
  const [display, setDisplay] = useState(0);
  
  useEffect(() => {
    let start = 0;
    const end = parseFloat(value);
    const increment = end / (duration / 16);
    const timer = setInterval(() => {
      start += increment;
      if (start >= end) {
        setDisplay(end);
        clearInterval(timer);
      } else {
        setDisplay(start);
      }
    }, 16);
    return () => clearInterval(timer);
  }, [value, duration]);
  
  return <span>{display.toFixed(decimals)}</span>;
};

// Health ring visualization
const HealthRing = ({ percent, size = 200 }) => {
  const strokeWidth = 12;
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference - (percent / 100) * circumference;
  
  const getHealthColor = (p) => {
    if (p >= 95) return '#4ADE80';  // Bright green
    if (p >= 90) return '#7DD87D';  // Soft green
    if (p >= 85) return '#DEB526';  // Rivian yellow
    if (p >= 80) return '#F59E0B';  // Amber
    return '#EF4444';               // Red
  };
  
  const getHealthStatus = (p) => {
    if (p >= 95) return 'Excellent';
    if (p >= 90) return 'Very Good';
    if (p >= 85) return 'Good';
    if (p >= 80) return 'Fair';
    return 'Needs Attention';
  };

  return (
    <div className="relative flex items-center justify-center" style={{ width: size, height: size }}>
      <svg width={size} height={size} className="transform -rotate-90">
        {/* Background ring */}
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="#2D4A3E"
          strokeWidth={strokeWidth}
        />
        {/* Progress ring */}
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke={getHealthColor(percent)}
          strokeWidth={strokeWidth}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          style={{ transition: 'stroke-dashoffset 1s ease-out' }}
        />
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <div className="text-5xl font-bold tracking-tight" style={{ color: '#F0F4F2' }}>
          <AnimatedNumber value={percent} />
          <span className="text-2xl" style={{ color: '#8FA696' }}>%</span>
        </div>
        <div className="text-sm mt-1 font-medium" style={{ color: '#C8D5CE' }}>{getHealthStatus(percent)}</div>
      </div>
    </div>
  );
};

// Stat card component
const StatCard = ({ icon: Icon, label, value, unit, subtext, color = '#DEB526' }) => (
  <div 
    className="rounded-2xl p-6 transition-all duration-200 hover:translate-y-[-2px]"
    style={{ 
      backgroundColor: '#142420', 
      border: '1px solid #2D4A3E',
    }}
  >
    <div className="flex items-center gap-2 mb-3">
      <Icon size={18} style={{ color }} />
      <span className="text-xs uppercase tracking-wider font-medium" style={{ color: '#8FA696' }}>{label}</span>
    </div>
    <div className="flex items-baseline gap-1">
      <span className="text-4xl font-bold tabular-nums" style={{ color: '#F0F4F2' }}>
        <AnimatedNumber value={value} decimals={unit === '%' ? 0 : 1} />
      </span>
      <span className="text-lg" style={{ color: '#8FA696' }}>{unit}</span>
    </div>
    {subtext && <p className="text-sm mt-2" style={{ color: '#8FA696' }}>{subtext}</p>}
  </div>
);

// Mini chart (simplified)
const MiniChart = ({ data, color = '#DEB526' }) => {
  const max = Math.max(...data);
  const min = Math.min(...data) - 2;
  const range = max - min;
  
  const points = data.map((val, i) => {
    const x = (i / (data.length - 1)) * 100;
    const y = 100 - ((val - min) / range) * 80;
    return `${x},${y}`;
  }).join(' ');
  
  return (
    <svg viewBox="0 0 100 100" className="w-full h-20" preserveAspectRatio="none">
      <defs>
        <linearGradient id="chartGradient" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stopColor={color} stopOpacity="0.3" />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      <polyline
        fill="url(#chartGradient)"
        stroke="none"
        points={`0,100 ${points} 100,100`}
      />
      <polyline
        fill="none"
        stroke={color}
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        points={points}
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
};

// Charging session item
const ChargingSession = ({ location, energy, duration, type, time }) => (
  <div className="flex items-center justify-between py-3" style={{ borderBottom: '1px solid #2D4A3E' }}>
    <div className="flex items-center gap-3">
      <div 
        className="w-10 h-10 rounded-xl flex items-center justify-center"
        style={{ backgroundColor: type === 'home' ? 'rgba(74, 222, 128, 0.15)' : 'rgba(96, 165, 250, 0.15)' }}
      >
        <Zap size={18} style={{ color: type === 'home' ? '#4ADE80' : '#60A5FA' }} />
      </div>
      <div>
        <p className="font-medium" style={{ color: '#F0F4F2' }}>{location}</p>
        <p className="text-sm" style={{ color: '#8FA696' }}>{time}</p>
      </div>
    </div>
    <div className="text-right">
      <p className="font-medium tabular-nums" style={{ color: '#F0F4F2' }}>{energy} kWh</p>
      <p className="text-sm" style={{ color: '#8FA696' }}>{duration}</p>
    </div>
  </div>
);

// Main dashboard
export default function RivianMateDashboard() {
  const [mounted, setMounted] = useState(false);
  
  useEffect(() => {
    setMounted(true);
  }, []);

  // Sample data
  const healthHistory = [131, 130.8, 130.5, 130.2, 129.8, 129.5, 129.2, 128.9, 128.6, 128.4];
  const batteryHealth = 97.2;
  const currentCapacity = 128.4;
  const originalCapacity = 131;
  const batteryLevel = 73;
  const range = 234;
  const odometer = 12456;

  // Color constants for consistent theming - Woodsy/Forest palette
  const colors = {
    // Backgrounds - deep forest tones
    bg: '#0D1A14',              // Deep forest black-green
    surface: '#142420',          // Dark forest green
    surfaceElevated: '#1A2F28',  // Slightly lighter forest
    border: '#2D4A3E',           // Muted green border
    
    // Text
    textPrimary: '#F0F4F2',      // Warm white with green tint
    textSecondary: '#C8D5CE',    // Soft sage
    textMuted: '#8FA696',        // Muted forest green
    
    // Accents
    accent: '#DEB526',           // Rivian yellow (keep for brand)
    accentHover: '#F5D45A',
    
    // Earth tones for variety
    bark: '#5C4A3D',             // Tree bark brown
    moss: '#4A6B5D',             // Moss green
    sage: '#7D9B8A',             // Sage green
    fern: '#3D5A4C',             // Fern green
  };
  
  return (
    <div className="min-h-screen" style={{ backgroundColor: colors.bg, color: colors.textPrimary, fontFamily: "'Outfit', 'Plus Jakarta Sans', system-ui, sans-serif" }}>
      {/* Subtle topographic background */}
      <div 
        className="fixed inset-0 opacity-[0.06] pointer-events-none"
        style={{
          backgroundImage: `url("data:image/svg+xml,%3Csvg width='100' height='100' viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M0 50 Q25 30 50 50 T100 50' fill='none' stroke='%234A6B5D' stroke-width='0.5'/%3E%3Cpath d='M0 60 Q25 40 50 60 T100 60' fill='none' stroke='%234A6B5D' stroke-width='0.5'/%3E%3Cpath d='M0 70 Q25 50 50 70 T100 70' fill='none' stroke='%234A6B5D' stroke-width='0.5'/%3E%3C/svg%3E")`,
          backgroundSize: '200px 200px'
        }}
      />
      
      {/* Header */}
      <header style={{ borderBottom: '1px solid #2D4A3E', backgroundColor: 'rgba(20, 36, 32, 0.95)', backdropFilter: 'blur(8px)' }} className="sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            {/* Simple compass-inspired logo */}
            <div className="w-10 h-10 relative">
              <div className="absolute inset-0 rotate-45 border-2 rounded-sm" style={{ borderColor: '#DEB526' }} />
              <div className="absolute inset-2 rotate-45" style={{ backgroundColor: '#DEB526' }} />
            </div>
            <span className="text-xl font-semibold tracking-tight" style={{ color: '#F0F4F2' }}>RivianMate</span>
          </div>
          <div className="flex items-center gap-4">
            <button className="p-2 rounded-lg transition-colors" style={{ color: '#8FA696' }}>
              <Settings size={20} />
            </button>
            <div className="w-9 h-9 rounded-full flex items-center justify-center text-sm font-semibold" style={{ background: 'linear-gradient(135deg, #DEB526, #B8940F)', color: '#0D1A14' }}>
              JD
            </div>
          </div>
        </div>
      </header>

      <main className="relative max-w-7xl mx-auto px-6 py-8">
        {/* Vehicle Header */}
        <div className={`mb-8 transition-all duration-700 ${mounted ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-4'}`}>
          <div className="flex items-center gap-4 mb-2">
            <div className="w-12 h-12 rounded-xl flex items-center justify-center" style={{ backgroundColor: 'rgba(222, 181, 38, 0.12)' }}>
              <Car size={24} style={{ color: '#DEB526' }} />
            </div>
            <div>
              <h1 className="text-2xl font-semibold" style={{ color: '#F0F4F2' }}>R1T · Compass Yellow</h1>
              <p style={{ color: '#C8D5CE' }}>Large Pack · Quad Motor · 2023</p>
            </div>
          </div>
          <div className="flex items-center gap-2 text-sm mt-3" style={{ color: '#C8D5CE' }}>
            <span className="w-2 h-2 rounded-full animate-pulse" style={{ backgroundColor: '#4ADE80' }} />
            <span>Online · Last updated just now</span>
          </div>
        </div>

        {/* Quick Stats Grid */}
        <div className={`grid grid-cols-1 md:grid-cols-3 gap-4 mb-8 transition-all duration-700 delay-100 ${mounted ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-4'}`}>
          <StatCard 
            icon={Battery} 
            label="State of Charge" 
            value={batteryLevel} 
            unit="%" 
            subtext="Charging complete"
            color="#22C55E"
          />
          <StatCard 
            icon={Gauge} 
            label="Est. Range" 
            value={range} 
            unit="mi"
            subtext="Based on recent driving"
            color="#5B8FA8"
          />
          <StatCard 
            icon={Zap} 
            label="Battery Health" 
            value={batteryHealth} 
            unit="%" 
            subtext={`${currentCapacity} of ${originalCapacity} kWh`}
            color="#DEB526"
          />
        </div>

        {/* Main Content Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Battery Health Section */}
          <div 
            className={`lg:col-span-2 rounded-2xl p-6 transition-all duration-700 delay-200 ${mounted ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-4'}`}
            style={{ backgroundColor: '#142420', border: '1px solid #2D4A3E' }}
          >
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-semibold" style={{ color: '#F0F4F2' }}>Battery Health</h2>
              <button className="text-sm flex items-center gap-1 transition-colors" style={{ color: '#DEB526' }}>
                View Details <ChevronRight size={16} />
              </button>
            </div>
            
            <div className="flex flex-col md:flex-row items-center gap-8">
              <HealthRing percent={batteryHealth} size={180} />
              
              <div className="flex-1 space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="rounded-xl p-4" style={{ backgroundColor: 'rgba(45, 74, 62, 0.4)' }}>
                    <p className="text-xs uppercase tracking-wider mb-1" style={{ color: '#8FA696' }}>Current Capacity</p>
                    <p className="text-2xl font-semibold tabular-nums" style={{ color: '#F0F4F2' }}>{currentCapacity} <span className="text-base" style={{ color: '#8FA696' }}>kWh</span></p>
                  </div>
                  <div className="rounded-xl p-4" style={{ backgroundColor: 'rgba(45, 74, 62, 0.4)' }}>
                    <p className="text-xs uppercase tracking-wider mb-1" style={{ color: '#8FA696' }}>Original Capacity</p>
                    <p className="text-2xl font-semibold tabular-nums" style={{ color: '#F0F4F2' }}>{originalCapacity} <span className="text-base" style={{ color: '#8FA696' }}>kWh</span></p>
                  </div>
                </div>
                
                <div className="rounded-xl p-4" style={{ backgroundColor: 'rgba(45, 74, 62, 0.4)' }}>
                  <div className="flex items-center justify-between mb-2">
                    <p className="text-xs uppercase tracking-wider" style={{ color: '#8FA696' }}>Capacity Over Time</p>
                    <p className="text-xs" style={{ color: '#8FA696' }}>Last 12 months</p>
                  </div>
                  <MiniChart data={healthHistory} color="#4ADE80" />
                </div>
              </div>
            </div>
            
            {/* Projections */}
            <div className="grid grid-cols-3 gap-4 mt-6 pt-6" style={{ borderTop: '1px solid #2D4A3E' }}>
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-lg flex items-center justify-center" style={{ backgroundColor: 'rgba(96, 165, 250, 0.15)' }}>
                  <TrendingDown size={18} style={{ color: '#60A5FA' }} />
                </div>
                <div>
                  <p className="text-xs" style={{ color: '#8FA696' }}>Degradation Rate</p>
                  <p className="font-semibold" style={{ color: '#F0F4F2' }}>0.8% <span style={{ color: '#8FA696', fontWeight: 'normal' }}>/ 10k mi</span></p>
                </div>
              </div>
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-lg flex items-center justify-center" style={{ backgroundColor: 'rgba(222, 181, 38, 0.15)' }}>
                  <Gauge size={18} style={{ color: '#DEB526' }} />
                </div>
                <div>
                  <p className="text-xs" style={{ color: '#8FA696' }}>At 100k miles</p>
                  <p className="font-semibold" style={{ color: '#F0F4F2' }}>~94.2%</p>
                </div>
              </div>
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-lg flex items-center justify-center" style={{ backgroundColor: 'rgba(74, 222, 128, 0.15)' }}>
                  <Shield size={18} style={{ color: '#4ADE80' }} />
                </div>
                <div>
                  <p className="text-xs" style={{ color: '#8FA696' }}>70% Threshold</p>
                  <p className="font-semibold" style={{ color: '#F0F4F2' }}>~450k mi</p>
                </div>
              </div>
            </div>
          </div>

          {/* Recent Charging */}
          <div 
            className={`rounded-2xl p-6 transition-all duration-700 delay-300 ${mounted ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-4'}`}
            style={{ backgroundColor: '#142420', border: '1px solid #2D4A3E' }}
          >
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold" style={{ color: '#F0F4F2' }}>Recent Charging</h2>
              <button className="text-sm flex items-center gap-1 transition-colors" style={{ color: '#DEB526' }}>
                View All <ChevronRight size={16} />
              </button>
            </div>
            
            <div className="space-y-1">
              <ChargingSession 
                location="Home" 
                energy="45.2" 
                duration="2h 15m" 
                type="home" 
                time="Today, 6:00 AM"
              />
              <ChargingSession 
                location="Rivian Adventure Network" 
                energy="62.8" 
                duration="28 min" 
                type="dcfc" 
                time="Yesterday, 3:45 PM"
              />
              <ChargingSession 
                location="Home" 
                energy="38.4" 
                duration="1h 45m" 
                type="home" 
                time="Jan 15, 11:30 PM"
              />
              <ChargingSession 
                location="Electrify America" 
                energy="71.2" 
                duration="32 min" 
                type="dcfc" 
                time="Jan 14, 2:15 PM"
              />
            </div>
          </div>
        </div>

        {/* Bottom Stats */}
        <div className={`grid grid-cols-2 md:grid-cols-4 gap-4 mt-6 transition-all duration-700 delay-400 ${mounted ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-4'}`}>
          <div className="rounded-xl p-4" style={{ backgroundColor: '#142420', border: '1px solid #2D4A3E' }}>
            <div className="flex items-center gap-2 mb-2">
              <MapPin size={14} style={{ color: '#8FA696' }} />
              <span className="text-xs uppercase tracking-wider" style={{ color: '#8FA696' }}>Odometer</span>
            </div>
            <p className="text-xl font-semibold tabular-nums" style={{ color: '#F0F4F2' }}>{odometer.toLocaleString()} <span className="text-sm" style={{ color: '#8FA696' }}>mi</span></p>
          </div>
          <div className="rounded-xl p-4" style={{ backgroundColor: '#142420', border: '1px solid #2D4A3E' }}>
            <div className="flex items-center gap-2 mb-2">
              <ThermometerSun size={14} style={{ color: '#8FA696' }} />
              <span className="text-xs uppercase tracking-wider" style={{ color: '#8FA696' }}>Cabin Temp</span>
            </div>
            <p className="text-xl font-semibold tabular-nums" style={{ color: '#F0F4F2' }}>68° <span className="text-sm" style={{ color: '#8FA696' }}>F</span></p>
          </div>
          <div className="rounded-xl p-4" style={{ backgroundColor: '#142420', border: '1px solid #2D4A3E' }}>
            <div className="flex items-center gap-2 mb-2">
              <Zap size={14} style={{ color: '#8FA696' }} />
              <span className="text-xs uppercase tracking-wider" style={{ color: '#8FA696' }}>Efficiency</span>
            </div>
            <p className="text-xl font-semibold tabular-nums" style={{ color: '#F0F4F2' }}>2.4 <span className="text-sm" style={{ color: '#8FA696' }}>mi/kWh</span></p>
          </div>
          <div className="rounded-xl p-4" style={{ backgroundColor: '#142420', border: '1px solid #2D4A3E' }}>
            <div className="flex items-center gap-2 mb-2">
              <Clock size={14} style={{ color: '#8FA696' }} />
              <span className="text-xs uppercase tracking-wider" style={{ color: '#8FA696' }}>Software</span>
            </div>
            <p className="text-xl font-semibold" style={{ color: '#F0F4F2' }}>2024.51.02</p>
          </div>
        </div>
      </main>
      
      {/* Footer */}
      <footer className="relative mt-12 py-6" style={{ borderTop: '1px solid #2D4A3E' }}>
        <div className="max-w-7xl mx-auto px-6 flex items-center justify-between text-sm" style={{ color: '#8FA696' }}>
          <p>RivianMate · Not affiliated with Rivian Automotive</p>
          <p>Data refreshes every 30 seconds when vehicle is awake</p>
        </div>
      </footer>
    </div>
  );
}
