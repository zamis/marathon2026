# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- Added the owner group pre-order review, consolidated submitted-cart totals, and confirmation flow.
- Added the group orders API service for session, status, owner-session, invite, and submission requests.
- Added silent 15-second/manual synchronization for Open owner group pre-orders, last-successful refresh feedback, and API-text submit error toasts.
- Added frontend regression tests for group-order endpoints and the Owner Review page's loading, error, consolidation, and submission flows.
- Added auth regression tests for owner-auth opt-in on group sessions and the unopted guest-request bypass.

### Changed
- Updated the group order page to load live reservation data and handle loading, error, empty, submitted, and closed states.

### Fixed
- Applied successful background status refreshes even when sessions refresh fails, retaining the last successful sessions data and stopping polling for non-Open groups.
- Kept the background-refresh failure warning visible with the last-good timestamp after a successful non-Open status update.
- Supported static ActivatedRoute snapshot parameters when no paramMap observable is provided.
- Preserved a text separator between consolidated dish names and quantities.
- Exposed the owner guest session ID as a string from the group-orders service while retaining the API response shape.
- Corrected singular grammar in the pending-participant notice while preserving its plural wording.
- Added an explicit owner-auth opt-in for the group sessions endpoint without changing guest-session request behavior.
