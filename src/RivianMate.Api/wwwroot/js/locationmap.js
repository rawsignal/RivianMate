// Location Map - Leaflet integration for managing charging locations
window.locationMap = {
    map: null,
    markersLayer: null,
    circlesLayer: null,
    markers: {}, // locationId -> marker
    circles: {}, // locationId -> circle
    dotNetRef: null,
    isInitialized: false,

    // Color palette for location markers
    colors: [
        '#FFAB00', // Amber (primary accent)
        '#4A90E2', // Blue
        '#22C55E', // Green
        '#EF4444', // Red
        '#8B5CF6', // Purple
        '#F97316', // Orange
        '#06B6D4', // Cyan
        '#EC4899'  // Pink
    ],

    // Initialize the map in the specified container
    initialize: function (containerId, dotNetReference) {
        // Clean up existing map if any
        if (this.map) {
            this.dispose();
        }

        this.dotNetRef = dotNetReference;

        const container = document.getElementById(containerId);
        if (!container) {
            console.error('Map container not found:', containerId);
            return false;
        }

        // Create the map centered on US (will be updated when locations load)
        this.map = L.map(containerId, {
            zoomControl: true,
            attributionControl: true
        }).setView([39.8283, -98.5795], 4); // Center of US

        // Add OpenStreetMap tiles
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(this.map);

        // Create layer groups for markers and circles
        this.circlesLayer = L.layerGroup().addTo(this.map);
        this.markersLayer = L.layerGroup().addTo(this.map);

        // Handle map click to add new location
        this.map.on('click', (e) => this.onMapClick(e));

        this.isInitialized = true;
        return true;
    },

    // Handle map click event
    onMapClick: function (e) {
        if (!this.dotNetRef) return;

        const lat = e.latlng.lat;
        const lng = e.latlng.lng;

        // Call Blazor method to handle new location
        this.dotNetRef.invokeMethodAsync('OnMapClicked', lat, lng);
    },

    // Add or update a location marker and circle
    setLocation: function (locationId, name, lat, lng, colorIndex) {
        if (!this.map || !this.isInitialized) return;

        const color = this.colors[colorIndex % this.colors.length];

        // Remove existing marker and circle if present
        this.removeLocation(locationId);

        // Create 100m radius circle
        const circle = L.circle([lat, lng], {
            radius: 100,
            color: color,
            fillColor: color,
            fillOpacity: 0.15,
            weight: 2,
            opacity: 0.6
        });
        this.circlesLayer.addLayer(circle);
        this.circles[locationId] = circle;

        // Create draggable marker
        const marker = L.marker([lat, lng], {
            draggable: true,
            icon: this.createMarkerIcon(name, color)
        });

        // Handle marker drag end
        marker.on('dragend', (e) => {
            const newPos = e.target.getLatLng();
            // Update circle position
            circle.setLatLng(newPos);
            // Notify Blazor of position change
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnMarkerDragged', locationId, newPos.lat, newPos.lng);
            }
        });

        // Show popup with location name
        marker.bindPopup(`<strong>${this.escapeHtml(name)}</strong><br><small>Drag to move</small>`);

        this.markersLayer.addLayer(marker);
        this.markers[locationId] = marker;
    },

    // Create a custom marker icon - GPS pin style with label
    createMarkerIcon: function (name, color) {
        return L.divIcon({
            className: 'location-marker',
            html: `<div class="location-pin-container">
                     <svg class="location-pin-svg" viewBox="0 0 24 36" width="32" height="48">
                       <path d="M12 0C5.4 0 0 5.4 0 12c0 7.2 12 24 12 24s12-16.8 12-24C24 5.4 18.6 0 12 0z" fill="${color}" stroke="white" stroke-width="2"/>
                       <circle cx="12" cy="12" r="5" fill="white" opacity="0.9"/>
                     </svg>
                     <div class="location-pin-label" style="background-color: ${color};">${this.escapeHtml(name)}</div>
                   </div>`,
            iconSize: [32, 48],
            iconAnchor: [16, 48],
            popupAnchor: [0, -48]
        });
    },

    // Remove a location marker and circle
    removeLocation: function (locationId) {
        if (this.markers[locationId]) {
            this.markersLayer.removeLayer(this.markers[locationId]);
            delete this.markers[locationId];
        }
        if (this.circles[locationId]) {
            this.circlesLayer.removeLayer(this.circles[locationId]);
            delete this.circles[locationId];
        }
    },

    // Clear all locations
    clearLocations: function () {
        if (this.markersLayer) {
            this.markersLayer.clearLayers();
        }
        if (this.circlesLayer) {
            this.circlesLayer.clearLayers();
        }
        this.markers = {};
        this.circles = {};
    },

    // Fit map bounds to show all locations
    fitToLocations: function (locations) {
        if (!this.map || !locations || locations.length === 0) {
            // Default to US center if no locations
            this.map.setView([39.8283, -98.5795], 4);
            return;
        }

        const bounds = L.latLngBounds(locations.map(l => [l.latitude, l.longitude]));
        this.map.fitBounds(bounds, {
            padding: [50, 50],
            maxZoom: 15
        });
    },

    // Center map on a specific location
    centerOnLocation: function (lat, lng, zoom) {
        if (!this.map) return;
        this.map.setView([lat, lng], zoom || 15);
    },

    // Highlight a specific location (open popup)
    highlightLocation: function (locationId) {
        const marker = this.markers[locationId];
        if (marker) {
            marker.openPopup();
        }
    },

    // Update marker position (for real-time updates during drag preview)
    updateMarkerPosition: function (locationId, lat, lng) {
        const marker = this.markers[locationId];
        const circle = this.circles[locationId];
        if (marker) {
            marker.setLatLng([lat, lng]);
        }
        if (circle) {
            circle.setLatLng([lat, lng]);
        }
    },

    // Invalidate size (call after container becomes visible)
    invalidateSize: function () {
        if (this.map) {
            this.map.invalidateSize();
        }
    },

    // Destroy the map
    dispose: function () {
        if (this.map) {
            this.map.remove();
            this.map = null;
            this.markersLayer = null;
            this.circlesLayer = null;
            this.markers = {};
            this.circles = {};
            this.isInitialized = false;
        }
    },

    // Helper to escape HTML
    escapeHtml: function (text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
};
