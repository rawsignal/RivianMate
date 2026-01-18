// Charge Map - Leaflet integration for RivianMate charging locations
window.chargeMap = {
    map: null,
    marker: null,

    // Initialize the map in the specified container
    initialize: function (containerId) {
        // Clean up existing map if any
        if (this.map) {
            this.map.remove();
            this.map = null;
            this.marker = null;
        }

        const container = document.getElementById(containerId);
        if (!container) {
            console.error('Map container not found:', containerId);
            return false;
        }

        // Create the map centered on a default location (will be updated when marker loads)
        this.map = L.map(containerId, {
            zoomControl: true,
            attributionControl: true
        }).setView([37.7749, -122.4194], 15);

        // Add OpenStreetMap tiles
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(this.map);

        return true;
    },

    // Set or update the charge location marker
    setMarker: function (latitude, longitude, locationName, chargeType) {
        if (!this.map) {
            console.warn('Cannot set marker: map not initialized');
            return false;
        }

        // Remove existing marker
        if (this.marker) {
            this.map.removeLayer(this.marker);
        }

        // Determine marker class based on charge type
        const markerClass = 'charge-marker charge-marker-' + chargeType;
        const iconHtml = this.getChargeIcon(chargeType);

        // Add marker
        this.marker = L.marker([latitude, longitude], {
            icon: L.divIcon({
                className: markerClass,
                html: '<div class="marker-inner">' + iconHtml + '</div>',
                iconSize: [40, 40],
                iconAnchor: [20, 20]
            })
        }).bindPopup('<strong>' + (locationName || 'Charging Location') + '</strong>');

        this.marker.addTo(this.map);

        // Center map on marker
        this.map.setView([latitude, longitude], 15);

        return true;
    },

    // Get icon SVG based on charge type
    getChargeIcon: function (chargeType) {
        if (chargeType === 'dcfc') {
            // Three lightning bolts for DCFC / Level 3
            return '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="4.96 5.2 0.4 13.36 4.5 13.36 4.04 18.8 8.6 10.64 4.5 10.64 4.96 5.2" /><polygon points="12.76 5.4 8.2 13.32 12.3 13.32 11.84 18.6 16.4 10.68 12.3 10.68 12.76 5.4" /><polygon points="20.5 5.5 16 13.42 20.05 13.42 19.6 18.7 24.1 10.78 20.05 10.78 20.5 5.5" /></svg>';
        } else if (chargeType === 'level2') {
            // Two lightning bolts for Level 2
            return '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="8.76 5.1 4.2 13.26 8.3 13.26 7.84 18.7 12.4 10.54 8.3 10.54 8.76 5.1" /><polygon points="17.26 5.4 12.7 13.32 16.8 13.32 16.34 18.6 20.9 10.68 16.8 10.68 17.26 5.4" /></svg>';
        } else {
            // Single lightning bolt for Level 1 / Home
            return '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/></svg>';
        }
    },

    // Clear the marker
    clear: function () {
        if (this.marker) {
            this.map.removeLayer(this.marker);
            this.marker = null;
        }
    },

    // Destroy the map
    dispose: function () {
        if (this.map) {
            this.map.remove();
            this.map = null;
            this.marker = null;
        }
    },

    // Invalidate size (call after container becomes visible)
    invalidateSize: function () {
        if (this.map) {
            this.map.invalidateSize();
        }
    }
};
