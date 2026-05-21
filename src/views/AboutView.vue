<template>
  <div class="flex flex-col items-center justify-center p-8">
    <h2 class="text-2xl font-bold text-gray-800 mb-4">About Flue</h2>

    <div class="bg-white rounded-lg shadow-md p-6 max-w-md">
      <p class="text-base text-gray-700 mb-4">
        Flue is a revolutionary cross-platform development toolchain that allows developers
        to write Vue 3 (TypeScript) + Tailwind CSS frontend code, and transpile it
        in real-time to native Flutter (Dart) projects.
      </p>

      <div class="border-t border-gray-200 pt-4 mt-2">
        <p class="text-sm text-gray-500 mb-2">Tech Stack:</p>
        <div class="flex flex-row flex-wrap gap-2">
          <span class="bg-blue-100 text-blue-700 text-xs px-2 py-1 rounded-full">.NET 10</span>
          <span class="bg-green-100 text-green-700 text-xs px-2 py-1 rounded-full">Vue 3</span>
          <span class="bg-purple-100 text-purple-700 text-xs px-2 py-1 rounded-full">Tailwind CSS</span>
          <span class="bg-cyan-100 text-cyan-700 text-xs px-2 py-1 rounded-full">Flutter</span>
          <span class="bg-orange-100 text-orange-700 text-xs px-2 py-1 rounded-full">Dart</span>
        </div>
      </div>
    </div>

    <button @click="router.back()" class="mt-6 text-blue-500 font-semibold">
      Go Back
    </button>
  </div>
</template>

<script lang="ts">
const router = useRouter();
</script>
