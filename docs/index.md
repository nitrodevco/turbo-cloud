---
layout: home

hero:
  name: Turbo
  text: A Habbo hotel you can build on
  tagline: The emulator, its plugin framework and its client, documented in one place.
  actions:
    - theme: brand
      text: Installation
      link: /installation
    - theme: alt
      text: Write a plugin
      link: /structure
    - theme: alt
      text: Packet lifecycle
      link: /lifecycle

features:
  - title: Plugins, not forks
    details: A plugin brings commands, packets, furni logic, wired boxes, web routes, settings, tables and a client module, and the server finds each one as the plugin loads.
    link: /extensions
  - title: Hot reload
    details: Save a file and the server compiles the plugin again and swaps it in, while the hotel keeps running.
    link: /plugin-loading
  - title: Tested like the hotel
    details: FakeHotel is the hotel API in memory with the same rules, so a plugin's tests run without a server.
    link: /testing
---
